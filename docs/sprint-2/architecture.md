# Arquitetura — Sprint 2

Arquitetura do Flow após a migração integral para MongoDB e a introdução das
funcionalidades inteligentes, de notificação e de observabilidade.

---

## 1. Visão geral

```text
┌──────────────────────────────────────────────────────────────┐
│  Mobile — React Native / Expo 54                             │
│  Operator · Manager · Leadership                             │
└───────────────────────────┬──────────────────────────────────┘
                            │ HTTPS · REST · JWT
┌───────────────────────────▼──────────────────────────────────┐
│  Flow.API — composition root                                 │
│  controllers · auth · middleware · rate limit · Swagger       │
│  health checks · correlation id                              │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Flow.Application — casos de uso                             │
│  commands · queries · handlers · validators · contratos      │
│  IUnitOfWork · I*Repository · IInnovationAssistant           │
│  IExecutiveInsightService · INotificationDispatcher          │
│  FlowScore (determinístico, sem dependência externa)         │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Flow.Domain — regras e máquinas de estado                   │
│  entidades · enums · invariantes · DomainException           │
└──────────────────────────────────────────────────────────────┘
                            ▲
┌───────────────────────────┴──────────────────────────────────┐
│  Flow.Infrastructure — adaptadores                           │
│  MongoDB.Driver · Identity stores Mongo · JWT                │
│  Gemini (Google.GenAI) · OneSignal · Outbox worker           │
│  Serilog · OpenTelemetry                                     │
└───────────────────────────┬──────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
   MongoDB rs0        Gemini API          OneSignal API
```

Regra de dependência inalterada: **as setas apontam para dentro**. `Domain` não conhece
`Infrastructure`; `Application` não conhece `MongoDB.Driver`.

---

## 2. O que muda em relação à Sprint 1

| Aspecto | Sprint 1 | Sprint 2 |
|---|---|---|
| Persistência | EF Core 8 + SQL Server | `MongoDB.Driver` 3.11.1 |
| Contrato de dados na Application | `IApplicationDbContext` com `DbSet<T>` | Repositórios por agregado + `IUnitOfWork` |
| Atomicidade | `SaveChangesAsync` único do EF | Sessão/transação Mongo explícita |
| Identity | `AddEntityFrameworkStores` | Stores Mongo implementados no projeto |
| Rastreio de mutação | Change tracking implícito | `UpdateAsync` explícito |
| Observabilidade | Logging padrão | Serilog + OpenTelemetry + health checks |
| Notificação | `INotificationService` sem implementação | Central persistida + Outbox + OneSignal |
| Inteligência | Ausente | Copiloto e Insights com Gemini |

O vazamento apontado no achado **A1** da matriz de compliance é eliminado: o pacote
`Microsoft.EntityFrameworkCore` sai de `Flow.Application.csproj`.

---

## 3. Persistência

### 3.1 Cliente

`MongoClient` é registrado como **singleton** — ele já mantém o pool de conexões
internamente e criá-lo por requisição é o erro clássico de uso do driver.
`IMongoDatabase` também é singleton; apenas o escopo transacional é `Scoped`.

### 3.2 Repositórios

Um repositório por agregado, definido na Application e implementado na Infrastructure:

```csharp
public interface IIdeaRepository
{
    Task<Idea?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Idea>> QueryAsync(IdeaFilter filter, CancellationToken ct);
    Task AddAsync(Idea idea, CancellationToken ct);
    Task UpdateAsync(Idea idea, CancellationToken ct);
    Task RemoveAsync(Guid id, CancellationToken ct);
}
```

`IAuditLogRepository` e `IProjectSnapshotRepository` expõem **apenas** `AppendAsync` e
leitura. Não existe caminho de código capaz de alterar ou apagar auditoria ou snapshot —
o append-only deixa de ser convenção e passa a ser propriedade do tipo.

### 3.3 Unit of Work

O ponto mais delicado da migração. O EF garantia atomicidade porque tudo ia em um
`SaveChanges`. Com escrita explícita, a garantia precisa ser reconstruída.

Contrato na Application, sem qualquer tipo do driver:

```csharp
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
```

Uso típico em um handler:

```csharp
await _unitOfWork.ExecuteAsync(async ct =>
{
    project.Block(request.Reason);
    await _projects.UpdateAsync(project, ct);
    await _snapshots.AppendAsync(snapshot, ct);
    await _audit.AppendAsync(audit, ct);
    await _notifications.EnqueueAsync(notification, ct);
}, cancellationToken);
```

Na Infrastructure, `MongoUnitOfWork` abre a sessão, inicia a transação, publica a sessão
em um `MongoSessionAccessor` **scoped e interno**, executa o delegate e faz commit ou
abort. Os repositórios leem a sessão desse acessor.

`IClientSessionHandle` nunca cruza a fronteira da Application — exigência explícita do
brief e condição para que a camada continue testável sem Mongo.

Chamadas aninhadas de `ExecuteAsync` reaproveitam a transação corrente em vez de abrir
outra, o que torna a composição segura.

### 3.4 Índices

Criados de forma idempotente no startup por um `MongoIndexInitializer`.
`CreateOneAsync` com o mesmo nome e a mesma chave é no-op, então subir a API várias vezes
não custa nada. A lista completa está em [`data-model.md`](data-model.md).

---

## 4. Identity sobre MongoDB

`UserManager`, `RoleManager`, `PasswordHasher`, JWT e refresh tokens são preservados.
Só o storage muda.

São implementados apenas os contratos realmente exercidos pelo produto:

| Contrato | Motivo |
|---|---|
| `IUserStore<User>` | Base obrigatória. |
| `IUserPasswordStore<User>` | Login com senha. |
| `IUserEmailStore<User>` | `FindByEmailAsync`, e-mail único. |
| `IUserRoleStore<User>` | Autorização por papel. |
| `IUserSecurityStampStore<User>` | Exigido pelo `UserManager` na criação e na troca de senha. |
| `IRoleStore<Role>` | Semeadura e checagem de papéis. |

Contratos não usados (2FA, lockout, claims, tokens de login externo) **não** são
implementados — implementá-los sem uso seria complexidade cerimonial.

Nenhum pacote comunitário de Identity para Mongo foi adotado: os avaliados estavam
desatualizados em relação ao driver 3.x e à linha 8 do Identity, e o custo de escrever
seis interfaces conhecidas é menor que o risco de depender de um adaptador parado.

---

## 5. Camada de aplicação

### 5.1 Validação

`FluentValidation` estava referenciado sem nenhum validador (achado A5). Passa a ser
usado de verdade através de um `ValidationBehavior` do MediatR, que roda antes do handler
e converte falhas em `ValidationException` → **422** com ProblemDetails.

A validação de entrada não substitui as invariantes de domínio; são camadas distintas.

### 5.2 FlowScore

Motor determinístico de priorização, independente do Gemini, documentado em
[`flowscore.md`](flowscore.md). Vive na Application porque é regra de produto, não
infraestrutura, e precisa ser testável sem rede.

### 5.3 Contratos das funcionalidades inteligentes

```csharp
public interface IInnovationAssistant
{
    Task<IdeaComparison> CompareIdeasAsync(IdeaComparisonRequest r, CancellationToken ct);
    Task<ProjectDraft> DraftProjectAsync(ProjectDraftRequest r, CancellationToken ct);
}

public interface IExecutiveInsightService
{
    Task<ExecutiveInsight> GenerateAsync(InsightRequest r, CancellationToken ct);
}
```

Tipos de retorno são **records do domínio da aplicação**, não tipos do SDK do Gemini.
Trocar de provedor é trocar a implementação em `Infrastructure`.

Regra inegociável: a inteligência **nunca escreve no banco**. Ela produz uma sugestão
estruturada; a escrita só acontece por um comando normal da Application, com
autorização, validação, domínio e auditoria.

---

## 6. Notificações

```text
        transação de domínio
                │
                ├── grava o agregado
                ├── grava AuditLog
                ├── grava Notification            (central do Flow)
                └── grava OutboxMessage           (intenção de push)
                │
             commit
                │
   OutboxDispatcherHostedService  (fora da transação)
                │
             OneSignal
```

A transação principal **não** depende da disponibilidade do OneSignal. Se o provedor
estiver fora, o commit acontece igual e o outbox retenta com backoff exponencial e
jitter até o limite, quando a mensagem vai para `DeadLettered`.

Idempotência vem do índice único em `dedupeKey`: reprocessar o mesmo evento falha na
inserção em vez de gerar push duplicado.

---

## 7. Resiliência externa

| Dependência | Timeout | Retry | Circuit breaker | Degradação |
|---|---|---|---|---|
| Gemini | sim | **não** (POST não idempotente) | sim | Erro tratado; núcleo do produto segue funcionando; FlowScore não depende do modelo. |
| OneSignal | sim | sim, com backoff e jitter (idempotente dos dois lados: `dedupeKey` aqui, `idempotency_key` lá) | sim | Outbox registra a tentativa e retenta depois. |
| MongoDB | sim | retry do próprio driver | — | Falha reportada em `/health/ready`. |

Retry cego em POST não idempotente é explicitamente evitado: repetir uma geração já
cobrada seria desperdício e poderia duplicar efeito.

---

## 8. Observabilidade

- **Serilog** com saída estruturada, enriquecida com `TraceId`, `SpanId`, `UserId` e
  `CorrelationId`. Chave de API, JWT e senha nunca são logados.
- **OpenTelemetry** para traces e métricas, instrumentando ASP.NET Core, `HttpClient` e
  MongoDB (`MongoDB.Driver.Core.Extensions.DiagnosticSources`).
- **Health checks**: `/health/live` (o processo respira) e `/health/ready` (Mongo
  alcançável).
- **Exportador OTLP opcional**: sem endpoint configurado, a instrumentação continua ativa
  em memória e a API sobe normalmente. A ausência de collector nunca derruba o serviço.

Métricas de negócio (`flow_ideas_submitted`, `flow_projects_blocked`, `flow_ai_requests`,
…) não recebem `userId` como label — cardinalidade alta em métrica é um problema
operacional, e a rastreabilidade por usuário já vive em `audit_logs` e nos traces.

Detalhes em [`observability.md`](observability.md).

---

## 9. Segurança

| Controle | Implementação |
|---|---|
| Erros | ProblemDetails RFC7807 com `traceId` |
| Validação | `ValidationBehavior` + invariantes de domínio |
| Autorização | Por papel no endpoint e por recurso no handler |
| CORS | Origens explícitas por configuração |
| Rate limiting | Rate limiter nativo do .NET 8: `/auth/*` por endereço, IA por usuário autenticado |
| Segredos | Somente variáveis de ambiente; nada de segredo real no repositório |
| JWT | Segredo validado em tamanho e em valor placeholder |
| Refresh token | Hash no banco, rotação a cada uso, revogação em cascata na reutilização |

---

## 10. Estrutura de projetos

Sem mudança de fronteira; mudam as dependências internas.

```text
src/
  Flow.Domain/           entidades, enums, invariantes
  Flow.Application/      casos de uso, contratos, FlowScore, validação
  Flow.Infrastructure/   Mongo, Identity stores, Gemini, OneSignal, observabilidade
  Flow.API/              controllers, middleware, auth, Swagger, health
tests/
  Flow.Domain.Tests/         invariantes e máquinas de estado
  Flow.Application.Tests/    handlers, FlowScore, contratos de IA
  Flow.Integration.Tests/    Mongo real via Testcontainers, replica set
  Flow.API.Tests/            HTTP ponta a ponta
mobile/                  cliente Expo
```

`Flow.Domain` deixa de referenciar `Microsoft.Extensions.Identity.Stores` diretamente
onde isso for possível sem quebrar `UserManager`; a dependência remanescente de
`IdentityUser<Guid>` fica isolada na entidade `User` e documentada como concessão
consciente ao ecossistema do ASP.NET Core Identity (achado A2).

---

## 11. Decisões arquiteturais registradas

| # | Decisão | Alternativa descartada | Motivo |
|---|---|---|---|
| D1 | Driver Mongo oficial | Provider EF Core sobre Mongo | Simular a arquitetura relacional anterior esconderia o modelo documental e o custo real das consultas. |
| D2 | `IUnitOfWork` com delegate | Expor `IClientSessionHandle` | Mantém o driver fora da Application e torna o limite transacional explícito no handler. |
| D3 | Stores de Identity próprios | Pacote comunitário | Os pacotes avaliados estavam defasados frente ao driver 3.x; seis interfaces conhecidas custam menos que um adaptador parado. |
| D4 | `blockedSince` materializado | Reconstruir por snapshots a cada dashboard | Remove um `$group` sobre coleção que só cresce, no caminho mais quente do produto. |
| D5 | Nome do autor e do dono desnormalizados | `$lookup` por linha | Listas com dezenas de itens não devem pagar junção por registro. |
| D6 | `DateTimeOffset` como `BsonType.DateTime` | Documento com ticks e offset | Mantém range query e operadores de data naturais; o domínio já grava tudo em UTC. |
| D7 | `decimal` como `Decimal128` | `double` | Precisão financeira em ROI, receita e custo. |
| D8 | Sem retry em chamada de geração | Retry genérico | POST não idempotente: repetir desperdiça cota e pode duplicar efeito. |
| D9 | Outbox para push | Chamar OneSignal dentro da transação | Disponibilidade de terceiro não pode decidir se uma aprovação de ideia é persistida. |
| D10 | FlowScore determinístico | Score gerado pelo modelo | O usuário precisa entender por que A ficou acima de B, e a priorização não pode depender de rede. |
