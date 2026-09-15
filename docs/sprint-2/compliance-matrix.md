# Sprint 2 — Compliance Matrix

Documento de controle da Sprint 2 do Flow. Cada requisito obrigatório é rastreado desde a
auditoria inicial do código até a evidência final de verificação.

- **Branch:** `sprint-2`
- **Baseline auditado:** `ff00816a3f5b6d5f09ab3576a2ce185785e046c4` (`master`, "chore: establish clean project baseline")
- **Baseline de testes:** 121 testes verdes (82 `Flow.Application.Tests` + 39 `Flow.API.Tests`)
- **Baseline de build:** `dotnet build` com êxito, 16 avisos `NU1603` (pin inexistente de `*.IdentityModel.Tokens 8.3.4`)
- **Data limite da entrega:** 21/09/2026 23:00

### Estado no fechamento

- **Build:** `dotnet build Flow.sln` — 0 erros, **0 avisos**
- **Testes:** **313** (124 Domain + 10 Application + 9 Architecture + 170 Integration contra MongoDB real), 0 falhas
- **API:** 54 endpoints, `openapi.json` exportado da própria aplicação
- **Mobile:** 23 telas, `tsc --noEmit` limpo, `expo-doctor` 18/18, bundle Android gerado
- **Compliance:** **110 de 113** requisitos `VERIFIED` — ver [seção 10](#10-estado-final-por-área)
- **Pendências:** 2, ambas por credencial externa — ver [seção 11](#11-pendências-reais)
- **CI:** `.github/workflows/ci.yml`, quatro jobs, **verde** — ver [seção 14.5](#145-ci)

> As seções 1 a 5 registram a auditoria e o plano do início da Sprint, e são mantidas como
> estavam: elas são o ponto de partida contra o qual o resultado é comparado. O estado
> **final** de cada requisito está nas seções 6 a 11.

## Estados

| Estado | Significado |
|---|---|
| `NOT_STARTED` | Não existe implementação. |
| `PARTIAL` | Existe implementação parcial ou que não cobre o requisito integralmente. |
| `IMPLEMENTED` | Implementado e compilando, ainda sem evidência de teste registrada. |
| `VERIFIED` | Implementado, testado e com evidência registrada nesta matriz. |

> A Sprint **não** é encerrada enquanto houver requisito obrigatório diferente de `VERIFIED`.

---

## 1. Sumário por área

| Área | Requisitos | Estado inicial predominante |
|---|---|---|
| AUTH | 8 | PARTIAL |
| STRATEGY | 9 | PARTIAL |
| IDEAS | 10 | PARTIAL |
| PROJECTS | 7 | PARTIAL |
| RESULTS | 6 | PARTIAL |
| DASHBOARD | 6 | PARTIAL |
| DATABASE | 7 | NOT_STARTED |
| MOBILE_INTEGRATION | 10 | PARTIAL |
| EXTERNAL_SERVICE | 5 | NOT_STARTED |
| OBSERVABILITY | 7 | NOT_STARTED |
| SECURITY | 9 | PARTIAL |
| AI_PLUS | 10 | NOT_STARTED |
| APK | 4 | NOT_STARTED |
| DOCUMENTATION | 10 | PARTIAL |
| DELIVERABLES | 5 | NOT_STARTED |
| **Total** | **113** | — |

---

## 2. Achados relevantes da auditoria inicial

Estes achados foram obtidos lendo o código real, não a documentação.

| # | Achado | Impacto |
|---|---|---|
| A1 | `Flow.Application` referencia `Microsoft.EntityFrameworkCore` e `IApplicationDbContext` expõe `DbSet<T>`. | Vazamento de infraestrutura para a camada de aplicação. Bloqueia a migração limpa para MongoDB. |
| A2 | `Flow.Domain` referencia `Microsoft.Extensions.Identity.Stores` porque `User : IdentityUser<Guid>`. | Contradiz "Domain sem dependência de framework" declarado em `ENGINEERING.md`. |
| A3 | Não existe endpoint para definir prioridade de ideia, embora `Idea.SetPriority` exista no domínio. | Capacidade documentada no README sem superfície de API. |
| A4 | Não existe endpoint para excluir ideia em `Draft`. | Requisito explícito do Operator na Sprint 2. |
| A5 | `FluentValidation` está referenciado em `Flow.Application.csproj` mas nenhum validador existe e nenhum behavior do MediatR está registrado. | Dependência morta; a validação hoje depende só do domínio e de `[ApiController]`. |
| A6 | `ICacheService`, `IAuthProvider` e `INotificationService` não possuem implementação registrada. | Interfaces sem uso real. `INotificationService` passa a ter uso real na Sprint 2. |
| A7 | `SaveChangesWithAuditAsync` chama `SaveChangesAsync` sem transação explícita. A atomicidade vem do `SaveChanges` único do EF. | Ao migrar para Mongo, a atomicidade precisa de sessão/transação explícita ou a garantia é perdida. |
| A8 | `mobile/src/api/client.ts` tem `API_BASE` fixo em `http://10.0.2.2:5153/api/v1` e limpa a sessão em **qualquer** 401. | Sem configuração por ambiente e sem refresh transparente. |
| A9 | A interface do mobile está em inglês. | A Sprint 2 exige pt-BR. |
| A10 | `Program.cs` não configura CORS, rate limiting, health checks, Serilog nem OpenTelemetry. | Requisitos de segurança e observabilidade ausentes. |
| A11 | Os testes de integração usam `Microsoft.EntityFrameworkCore.InMemory`. | Precisa migrar para Mongo real descartável (Testcontainers). |
| A12 | `ProjectSnapshot` e `AuditLog` são append-only por convenção, não por restrição técnica. | Precisa ser reforçado na camada de persistência Mongo. |
| A13 | Docker Desktop instalado porém com o daemon parado no ambiente de desenvolvimento. | Bloqueia replica set, Testcontainers e build de imagem até ser iniciado. |
| A14 | `victory-native@42.x` exige `@shopify/react-native-skia >=2.6.0 <3.0.0`; o Expo SDK 54 fixa `2.2.12`. | Conflito real de peer dependency. Exige spike antes de adotar. A linha `41.x` (`skia >=1.2.3`) é a candidata compatível. |
| A15 | 16 avisos `NU1603`: pin de `Microsoft.IdentityModel.Tokens` e `System.IdentityModel.Tokens.Jwt` em `8.3.4`, versão inexistente. | Ruído de build; corrigir o pin para uma versão publicada. |

---

## 3. Decisões de pesquisa confirmadas

Versões verificadas contra os registries oficiais em 09/09/2026, não por memória.

| Item | Decisão | Verificação |
|---|---|---|
| Driver Mongo | `MongoDB.Driver` 3.11.1 | NuGet flat-container. Driver oficial. Sem provider EF sobre Mongo. |
| SDK Gemini | `Google.GenAI` 1.21.0 | NuGet: publicado pelo Google, prefixo reservado, repositório `googleapis/dotnet-genai`, suporta `net8.0`. |
| Modelo Gemini | `gemini-3.8-flash` | Confirmado GA na documentação oficial do Gemini API. Contexto de 1M tokens, thinking configurável. |
| Testes Mongo | `Testcontainers.MongoDb` 4.15.0 | NuGet. Usado com replica set de nó único para exercitar transações reais. |
| Observabilidade | `Serilog.AspNetCore` 10.0.0, `OpenTelemetry.*` 1.18.0 | NuGet. |
| Instrumentação Mongo | `MongoDB.Driver.Core.Extensions.DiagnosticSources` 3.0.0 | NuGet. Compatível com a linha 3.x do driver. |
| Health check Mongo | `AspNetCore.HealthChecks.MongoDb` 9.0.0 | NuGet. |
| Push | `react-native-onesignal` 5.5.10 + `onesignal-expo-plugin` 2.7.1 | npm. Exige development build; não funciona em Expo Go. |
| Charts | Spike obrigatório antes de fixar (ver A14) | npm: peer deps de `victory-native` 41.x vs 42.x e `bundledNativeModules.json` do SDK 54. |

---

## 4. Matriz detalhada

Cada requisito registra: **Fonte/requisito · Estado atual · Evidência no código · Gap · Implementação proposta · Teste necessário · Evidência final · Status**.

O campo *Evidência final* é preenchido no fechamento de cada fase.

---

### 4.1 AUTH

#### AUTH-01 — Registro público cria Operator
- **Fonte/requisito:** Challenge (autenticação); brief §7.
- **Estado atual:** IMPLEMENTED no baseline.
- **Evidência no código:** `src/Flow.Application/Auth/Commands/Register/RegisterCommandHandler.cs`; `AuthController.Register`.
- **Gap:** Depende de `UserManager` sobre EF; precisa continuar funcionando sobre stores Mongo.
- **Implementação proposta:** Preservar o handler; trocar apenas o storage do Identity.
- **Teste necessário:** Unit do handler + integração `POST /auth/register` retornando papel `Operator`.
- **Evidência final:** _pendente_
- **Status:** `PARTIAL`

#### AUTH-02 — Login com JWT
- **Fonte/requisito:** Challenge (JWT).
- **Estado atual:** IMPLEMENTED no baseline.
- **Evidência no código:** `LoginCommandHandler.cs`; `Flow.Infrastructure/Auth/JwtTokenService.cs`.
- **Gap:** Nenhum funcional; validar após a migração.
- **Implementação proposta:** Manter; adicionar validação de força do segredo.
- **Teste necessário:** Login válido, credencial inválida, claims de papel.
- **Evidência final:** _pendente_
- **Status:** `PARTIAL`

#### AUTH-03 — Refresh token
- **Fonte/requisito:** Challenge; brief §18.
- **Estado atual:** IMPLEMENTED no baseline.
- **Evidência no código:** `RefreshTokenCommandHandler.cs`; `Flow.Domain/Entities/RefreshToken.cs`.
- **Gap:** Token persistido em claro; sem rotação explícita.
- **Implementação proposta:** Persistir hash do token, emitir novo par a cada refresh e revogar o anterior.
- **Teste necessário:** Refresh válido, expirado, revogado e reuso após rotação.
- **Evidência final:** _pendente_
- **Status:** `PARTIAL`

#### AUTH-04 — Logout com revogação
- **Estado atual:** IMPLEMENTED. **Evidência:** `LogoutCommandHandler.cs`.
- **Gap:** Revalidar sobre Mongo. **Proposta:** manter. **Teste:** logout revoga e o refresh seguinte falha.
- **Evidência final:** _pendente_ · **Status:** `PARTIAL`

#### AUTH-05 — Três perfis e autorização por nível
- **Estado atual:** IMPLEMENTED. **Evidência:** `UserRole.cs`; `[Authorize(Roles = ...)]` nos controllers.
- **Gap:** A cobertura de teste de 403 é parcial. **Proposta:** manter e ampliar os testes.
- **Teste:** matriz de acesso por papel em todos os endpoints sensíveis.
- **Evidência final:** _pendente_ · **Status:** `PARTIAL`

#### AUTH-06 — Rotação e revogação seguras de refresh token
- **Estado atual:** `PARTIAL`. **Gap:** sem rotação nem hash.
- **Proposta:** hash SHA-256 no armazenamento, índice único, rotação obrigatória e revogação em cascata na reutilização.
- **Teste:** o reuso de um token rotacionado deve falhar.
- **Evidência final:** _pendente_ · **Status:** `PARTIAL`

#### AUTH-07 — Seeds de demonstração idempotentes sob `SEED_DEMO_DATA`
- **Estado atual:** `NOT_STARTED`. O baseline só semeia os papéis do Identity (`Program.cs`).
- **Proposta:** seeder idempotente com os três usuários de demonstração e narrativa de dados.
- **Teste:** executar duas vezes não duplica dados.
- **Evidência final:** _pendente_ · **Status:** `NOT_STARTED`

#### AUTH-08 — ASP.NET Core Identity sobre MongoDB
- **Estado atual:** `NOT_STARTED`. **Evidência:** `AddEntityFrameworkStores<ApplicationDbContext>()` em `Flow.Infrastructure/DependencyInjection.cs`.
- **Proposta:** implementar `IUserStore`, `IUserPasswordStore`, `IUserEmailStore`, `IUserRoleStore`, `IUserSecurityStampStore` e `IRoleStore` sobre `MongoDB.Driver`, sem pacote comunitário.
- **Teste:** integração cobrindo criação, busca por e-mail normalizado, atribuição de papel e verificação de senha.
- **Evidência final:** _pendente_ · **Status:** `NOT_STARTED`

---

### 4.2 STRATEGY

#### STR-01 — CRUD de diretriz restrito a Leadership
- **Estado atual:** IMPLEMENTED. **Evidência:** `GuidelinesController.cs` com `[Authorize(Roles = "Leadership")]` em `POST`, `PUT` e `DELETE`.
- **Gap:** nenhum. **Teste:** CRUD por Leadership e 403 para os demais.
- **Evidência final:** _pendente_ · **Status:** `PARTIAL`

#### STR-02 — Leitura por Operator e Manager
- **Estado atual:** IMPLEMENTED. **Evidência:** `[Authorize]` no controller, sem restrição de papel no `GET`.
- **Evidência final:** _pendente_ · **Status:** `PARTIAL`

#### STR-03 — `Category`
- **Estado atual:** `NOT_STARTED`. **Evidência:** `StrategicGuideline.cs` só tem `Title`, `Description` e `CreatedBy`.
- **Proposta:** adicionar `Category` com validação de domínio. **Teste:** filtro por categoria.
- **Evidência final:** _pendente_ · **Status:** `NOT_STARTED`

#### STR-04 — `Campaign`
- **Estado atual:** `NOT_STARTED`. **Proposta:** campo opcional e agrupamento no dashboard.
- **Teste:** performance por campanha no dashboard. · **Status:** `NOT_STARTED`

#### STR-05 — `ValidFrom` e `ValidUntil` com vigência derivada
- **Estado atual:** `NOT_STARTED`. **Proposta:** vigência calculada a partir do período, sem `IsActive` mutável.
- **Teste:** limites de vigência (antes, durante, depois e `ValidUntil` nulo). · **Status:** `NOT_STARTED`

#### STR-06 — Histórico consultável
- **Estado atual:** `NOT_STARTED`. **Proposta:** coleção `strategic_guideline_history` append-only alimentada em cada alteração.
- **Teste:** um update gera entrada de histórico. · **Status:** `NOT_STARTED`

#### STR-07 — Endpoint de estratégia vigente
- **Estado atual:** `NOT_STARTED`. **Proposta:** `GET /api/v1/guidelines/current`.
- **Teste:** retorna apenas as vigentes na data de referência. · **Status:** `NOT_STARTED`

#### STR-08 — Filtros por categoria, vigência e campanha
- **Estado atual:** `NOT_STARTED`. **Proposta:** query string em `GET /guidelines` e índices Mongo.
- **Teste:** cada filtro isolado e combinado. · **Status:** `NOT_STARTED`

#### STR-09 — Validação de existência ao vincular ideia ou projeto
- **Estado atual:** `PARTIAL`. **Evidência:** `Idea.LinkedGuidelineId` existe, mas `CreateIdeaCommandHandler` não valida a diretriz.
- **Proposta:** validar existência e registrar a diretriz aplicável na conversão para projeto.
- **Teste:** vincular diretriz inexistente retorna 404 ou 422. · **Status:** `PARTIAL`

---

### 4.3 IDEAS

#### IDEA-01 — Criar, editar e excluir Draft
- **Estado atual:** `PARTIAL`. **Evidência:** `CreateIdea` e `UpdateIdea` existem; **não existe** comando nem rota de exclusão (achado A4).
- **Proposta:** `DeleteIdeaCommand` restrito a Draft e ao autor.
- **Teste:** excluir Draft próprio, 403 para terceiros e 409 fora de Draft. · **Status:** `PARTIAL`

#### IDEA-02 — Enviar para análise
- **Estado atual:** IMPLEMENTED. **Evidência:** `SubmitIdeaCommandHandler.cs`. · **Status:** `PARTIAL`

#### IDEA-03 — Fila do gestor com filtros
- **Estado atual:** `PARTIAL`. **Evidência:** `GetIdeasQueryHandler` lista sem filtros ricos.
- **Proposta:** filtros por status, prioridade, score e diretriz, com paginação. · **Status:** `PARTIAL`

#### IDEA-04 — Comentários
- **Estado atual:** IMPLEMENTED. **Evidência:** `AddIdeaComment*` e `GetIdeaComments*`. · **Status:** `PARTIAL`

#### IDEA-05 — Priorização pelo gestor
- **Estado atual:** `PARTIAL` (achado A3). **Evidência:** `Idea.SetPriority` sem endpoint.
- **Proposta:** `PATCH /ideas/{id}/priority`. **Teste:** priorizar e auditar. · **Status:** `PARTIAL`

#### IDEA-06 — `Score` 0..100 distinto de `Priority`
- **Estado atual:** `NOT_STARTED`. **Proposta:** campo `Score` com validação de faixa e endpoint dedicado.
- **Teste:** limites 0, 100, -1 e 101. · **Status:** `NOT_STARTED`

#### IDEA-07 — Aprovar e rejeitar
- **Estado atual:** IMPLEMENTED. **Evidência:** `ApproveIdeaCommandHandler.cs` e `RejectIdeaCommandHandler.cs`. · **Status:** `PARTIAL`

#### IDEA-08 — Comparação de ideias
- **Estado atual:** `NOT_STARTED`. **Proposta:** query de comparação lado a lado com os componentes do FlowScore. · **Status:** `NOT_STARTED`

#### IDEA-09 — Auditoria de decisões gerenciais
- **Estado atual:** IMPLEMENTED para aprovar e rejeitar. **Gap:** priorização e score também precisam auditar. · **Status:** `PARTIAL`

#### IDEA-10 — FlowScore explicável
- **Estado atual:** `NOT_STARTED`. **Proposta:** motor determinístico 0..100 com componentes visíveis e fórmula documentada em `docs/sprint-2/flowscore.md`; independente do Gemini; a entrada manual permanece soberana.
- **Teste:** unit da fórmula, monotonicidade por dimensão, limites e ordenação A vs B. · **Status:** `NOT_STARTED`

---

### 4.4 PROJECTS

#### PRJ-01 — State machine preservada
- **Estado atual:** IMPLEMENTED. **Evidência:** `Flow.Domain/Entities/Project.cs`.
- **Gap:** não regredir durante a migração. **Teste:** suíte de transições válidas e inválidas. · **Status:** `PARTIAL`

#### PRJ-02 — `StrategicGuidelineId`
- **Estado atual:** `NOT_STARTED`. **Proposta:** vínculo explícito registrado na criação e na conversão. · **Status:** `NOT_STARTED`

#### PRJ-03 — `Stage` distinto de `Status`
- **Estado atual:** `NOT_STARTED`. **Proposta:** enum `ProjectStage` (Discovery → Planning → Execution → Validation → Rollout) documentado. · **Status:** `NOT_STARTED`

#### PRJ-04 — `ProgressPercentage` 0..100 com operação dedicada
- **Estado atual:** `NOT_STARTED`. **Proposta:** comando próprio; `Completed` implica 100.
- **Teste:** limites, invariante de conclusão e auditoria. · **Status:** `NOT_STARTED`

#### PRJ-05 — Criação manual e a partir de ideia aprovada
- **Estado atual:** IMPLEMENTED. **Evidência:** `CreateProjectCommandHandler.cs` e `ConvertIdeaToProjectCommandHandler.cs`. · **Status:** `PARTIAL`

#### PRJ-06 — Rastreabilidade `strategy → idea → project → result`
- **Estado atual:** `PARTIAL`. **Gap:** falta o elo de estratégia. · **Status:** `PARTIAL`

#### PRJ-07 — Snapshots e timeline
- **Estado atual:** IMPLEMENTED. **Evidência:** `GetProjectSnapshots*` e `GetProjectTimeline*`.
- **Gap:** a imutabilidade precisa ser garantida na camada Mongo. · **Status:** `PARTIAL`

---

### 4.5 RESULTS

#### RES-01 — Estimated e Actual independentes
- **Estado atual:** IMPLEMENTED. **Evidência:** `Result.SetEstimated` e `Result.SetActual`. · **Status:** `PARTIAL`

#### RES-02 — ROI e `PaybackPeriodMonths`
- **Estado atual:** IMPLEMENTED. **Evidência:** `Result.ComputeRoi`; divisão por zero retorna `null`. · **Status:** `PARTIAL`

#### RES-03 — `ProductivityGainPercent` · RES-04 — `TimeSavedHours` · RES-05 — `QualityGainPercent`
- **Estado atual:** `NOT_STARTED` nos três. **Proposta:** adicionar ao agregado com validação de faixa e refletir no dashboard.
- **Teste:** limites e persistência independente de Estimated e Actual. · **Status:** `NOT_STARTED`

#### RES-06 — Validações de domínio
- **Estado atual:** `PARTIAL`. **Gap:** valores negativos não são rejeitados hoje.
- **Proposta:** invariantes explícitas. · **Status:** `PARTIAL`

---

### 4.6 DASHBOARD

#### DASH-01 — `GET /dashboard/summary` robusto
- **Estado atual:** `PARTIAL`. **Evidência:** `GetDashboardSummaryQueryHandler.cs` cobre 12 métricas.
- **Gap:** faltam stage, atrasados, em risco, distribuição por estratégia, campanha, rankings, tendências, produtividade e horas economizadas.
- **Teste:** base vazia e base realista. · **Status:** `PARTIAL`

#### DASH-02 — Datasets prontos para visualização
- **Estado atual:** `NOT_STARTED`. **Proposta:** séries e distribuições prontas para gráfico, sem reagregação no mobile. · **Status:** `NOT_STARTED`

#### DASH-03 — Aggregation pipelines sem N+1
- **Estado atual:** `NOT_STARTED`. **Evidência do problema:** o baseline faz cerca de 8 roundtrips sequenciais.
- **Proposta:** `$facet` para consolidar em poucas idas ao banco. · **Status:** `NOT_STARTED`

#### DASH-04 — `GET /dashboard/projects/{id}` · DASH-05 — `GET /dashboard/strategies/{id}`
- **Estado atual:** `NOT_STARTED` em ambos. · **Status:** `NOT_STARTED`

#### DASH-06 — Rankings e tendências temporais
- **Estado atual:** `NOT_STARTED`. · **Status:** `NOT_STARTED`

---

### 4.7 DATABASE

#### DB-01 — Remoção operacional de EF Core e SQL Server
- **Estado atual:** `NOT_STARTED`. **Evidência:** `ApplicationDbContext`, 3 migrations, `UseSqlServer` e `Microsoft.EntityFrameworkCore.SqlServer`.
- **Proposta:** remover `DbContext`, configurations, migrations e pacotes EF de `src/`.
- **Teste:** ausência de referência a EF em `src/` verificada por busca. · **Status:** `NOT_STARTED`

#### DB-02 — `MongoDB.Driver` oficial
- **Estado atual:** `NOT_STARTED`. **Proposta:** driver 3.11.1 com `MongoClient` singleton. · **Status:** `NOT_STARTED`

#### DB-03 — Modelo documental derivado dos access patterns
- **Estado atual:** `NOT_STARTED`. **Proposta:** documentar em `docs/sprint-2/data-model.md` antes de codificar. · **Status:** `NOT_STARTED`

#### DB-04 — Índices explícitos
- **Estado atual:** `NOT_STARTED`. **Proposta:** criação idempotente no startup para os padrões listados no brief §5. · **Status:** `NOT_STARTED`

#### DB-05 — Transações multi-documento em replica set
- **Estado atual:** `NOT_STARTED`. **Proposta:** replica set de nó único em desenvolvimento e teste; compose com `rs.initiate`. · **Status:** `NOT_STARTED`

#### DB-06 — Unit of Work sem vazar `IClientSessionHandle`
- **Estado atual:** `NOT_STARTED`. **Proposta:** `IUnitOfWork.ExecuteAsync(delegate)` na Application, com a sessão resolvida por um acessor interno da Infrastructure.
- **Teste:** rollback do agregado quando a escrita de auditoria falha. · **Status:** `NOT_STARTED`

#### DB-07 — `AuditLog` e `ProjectSnapshot` append-only
- **Estado atual:** `PARTIAL` por convenção. **Proposta:** repositórios de escrita expõem apenas append; nenhuma rota de update ou delete.
- **Teste:** ausência de operação de update e delete nesses repositórios. · **Status:** `PARTIAL`

---

### 4.8 MOBILE_INTEGRATION

#### MOB-01 — Base URL por ambiente
- **Estado atual:** `NOT_STARTED` (achado A8). **Proposta:** `app.config.ts` com `extra` e variáveis por perfil EAS. · **Status:** `NOT_STARTED`

#### MOB-02 — Refresh transparente com single-flight
- **Estado atual:** `NOT_STARTED` (achado A8). **Proposta:** interceptor com promessa compartilhada; a falha limpa a sessão.
- **Teste:** múltiplos 401 concorrentes disparam um único refresh. · **Status:** `NOT_STARTED`

#### MOB-03 — Jornada Operator completa
- **Estado atual:** `PARTIAL`. **Evidência:** 3 telas (`MyIdeas`, `SubmitIdea`, `IdeaDetail`).
- **Gap:** Home, estratégia vigente, edição e exclusão de Draft, comentários, pontos e notificações. · **Status:** `PARTIAL`

#### MOB-04 — Jornada Manager completa
- **Estado atual:** `PARTIAL`. **Evidência:** 4 telas. **Gap:** filtros, comparação, scoring, priorização, copiloto, progresso, etapa, resultados e notificações. · **Status:** `PARTIAL`

#### MOB-05 — Jornada Leadership completa
- **Estado atual:** `PARTIAL`. **Evidência:** 1 tela (`DashboardScreen`). **Gap:** gráficos, tendências, risco, ranking, insights, CRUD de estratégia, histórico e campanhas. · **Status:** `PARTIAL`

#### MOB-06 — Compartilhado (perfil, notificações, estratégias, logout)
- **Estado atual:** `PARTIAL`. **Gap:** só existe login e logout. · **Status:** `PARTIAL`

#### MOB-07 — Charts
- **Estado atual:** `NOT_STARTED`. **Bloqueio conhecido:** achado A14. **Proposta:** spike de compatibilidade antes de fixar a biblioteca. · **Status:** `NOT_STARTED`

#### MOB-08 — Loading, empty e error states com retry
- **Estado atual:** `PARTIAL`. · **Status:** `PARTIAL`

#### MOB-09 — Interface em pt-BR
- **Estado atual:** `NOT_STARTED` (achado A9). · **Status:** `NOT_STARTED`

#### MOB-10 — API client tipado com ProblemDetails
- **Estado atual:** `PARTIAL`. **Gap:** os erros viram `Error` genérico; sem cancelamento nem tratamento por status. · **Status:** `PARTIAL`

---

### 4.9 EXTERNAL_SERVICE

#### EXT-01 — Push via OneSignal
- **Estado atual:** `NOT_STARTED`. **Dependência externa:** credenciais OneSignal e FCM.
- **Proposta:** cliente server-side e plugin Expo; a validação live é marcada como pendente se faltar credencial. · **Status:** `NOT_STARTED`

#### EXT-02 — Central de notificações persistida
- **Estado atual:** `NOT_STARTED`. **Proposta:** coleção `notifications` com read/unread e deep link. · **Status:** `NOT_STARTED`

#### EXT-03 — Outbox e HostedService
- **Estado atual:** `NOT_STARTED`. **Proposta:** `notification_outbox` gravado na mesma transação do domínio; o worker despacha fora dela. · **Status:** `NOT_STARTED`

#### EXT-04 — Retry, backoff, dead-letter e idempotência
- **Estado atual:** `NOT_STARTED`. **Teste:** falha transitória reprocessa; falha permanente vai para dead-letter; o reprocesso não duplica. · **Status:** `NOT_STARTED`

#### EXT-05 — Timeout, cancellation e circuit breaker
- **Estado atual:** `NOT_STARTED`. **Proposta:** resiliência do .NET 8 sobre `IHttpClientFactory`. · **Status:** `NOT_STARTED`

---

### 4.10 OBSERVABILITY

| ID | Requisito | Estado | Gap | Status |
|---|---|---|---|---|
| OBS-01 | Serilog estruturado | O baseline usa o logging padrão | Sem log estruturado nem enriquecimento | `NOT_STARTED` |
| OBS-02 | OpenTelemetry Traces | Ausente | ASP.NET Core, HttpClient e Mongo | `NOT_STARTED` |
| OBS-03 | OpenTelemetry Metrics | Ausente | Métricas técnicas e de negócio | `NOT_STARTED` |
| OBS-04 | `/health/live` e `/health/ready` | Ausente | — | `NOT_STARTED` |
| OBS-05 | Correlation e trace id | Ausente | Propagar e devolver no ProblemDetails | `NOT_STARTED` |
| OBS-06 | Métricas de domínio (`flow_*`) | Ausente | Sem labels de alta cardinalidade | `NOT_STARTED` |
| OBS-07 | OTLP configurável e opcional | Ausente | A ausência de collector não pode derrubar a API | `NOT_STARTED` |

---

### 4.11 SECURITY

| ID | Requisito | Estado | Evidência / Gap | Status |
|---|---|---|---|---|
| SEC-01 | ProblemDetails RFC7807 | Implementado | `Middleware/ExceptionHandlingMiddleware.cs` | `PARTIAL` |
| SEC-02 | Validação de entrada | Parcial | FluentValidation referenciado sem uso (achado A5) | `PARTIAL` |
| SEC-03 | Autorização por recurso | Parcial | Verificar leitura de ideia de terceiros em `GetIdeaByIdQueryHandler` | `PARTIAL` |
| SEC-04 | CORS explícito | Ausente | Sem `AddCors` em `Program.cs` | `NOT_STARTED` |
| SEC-05 | Rate limiting | Ausente | Necessário em auth e nos endpoints de IA | `NOT_STARTED` |
| SEC-06 | Secrets por variável de ambiente | Parcial | `appsettings.json` traz segredo placeholder | `PARTIAL` |
| SEC-07 | Validação do segredo JWT | Parcial | Só checa o prefixo `CHANGE-THIS` fora de Development | `PARTIAL` |
| SEC-08 | Refresh seguro, expiração, revogação e rotação | Parcial | Ver AUTH-06 | `PARTIAL` |
| SEC-09 | Logs sem segredos | Parcial | Sem redação explícita de chave e JWT | `PARTIAL` |

---

### 4.12 AI_PLUS

| ID | Requisito | Estado | Proposta | Status |
|---|---|---|---|---|
| AI-01 | Contratos neutros na Application | Ausente | `IInnovationAssistant` e `IExecutiveInsightService` | `NOT_STARTED` |
| AI-02 | Copiloto contextual com structured output | Ausente | Comparação, riscos, trade-offs e justificativas | `NOT_STARTED` |
| AI-03 | Function calling sobre dados autorizados | Ausente | `getCurrentStrategies`, `getIdeasUnderReview`, `compareIdeas` e afins | `NOT_STARTED` |
| AI-04 | ProjectDraft com confirmação humana | Ausente | O modelo nunca escreve no banco | `NOT_STARTED` |
| AI-05 | Insights executivos | Ausente | `POST /dashboard/insights` estruturado | `NOT_STARTED` |
| AI-06 | Evidência obrigatória, sem inventar métrica | Ausente | Deve declarar evidência insuficiente | `NOT_STARTED` |
| AI-07 | Timeout, cancellation, rate limit e resiliência | Ausente | Sem retry cego em POST não idempotente | `NOT_STARTED` |
| AI-08 | Telemetria de latência, sucesso, tokens e modelo | Ausente | Nunca logar chave, JWT ou conteúdo sensível | `NOT_STARTED` |
| AI-09 | Governança de execuções | Ausente | Coleção `assistant_runs` com solicitante, modelo, resultado e aceite | `NOT_STARTED` |
| AI-10 | `GEMINI_API_KEY` apenas server-side | Ausente | Proibido no mobile | `NOT_STARTED` |

---

### 4.13 APK

| ID | Requisito | Estado | Observação | Status |
|---|---|---|---|---|
| APK-01 | `eas.json` com profile APK | Ausente | `preview` ou `internal` gerando `.apk` | `NOT_STARTED` |
| APK-02 | `expo-doctor` e `tsc --noEmit` limpos | Não executado no baseline | Gate de qualidade | `NOT_STARTED` |
| APK-03 | Build Android | Ausente | Depende de credencial EAS | `NOT_STARTED` |
| APK-04 | Validação do APK em dispositivo | Ausente | Documentar honestamente se bloqueado por credencial | `NOT_STARTED` |

---

### 4.14 DOCUMENTATION

| ID | Arquivo | Estado | Status |
|---|---|---|---|
| DOC-01 | `docs/sprint-2/compliance-matrix.md` | Este documento | `IMPLEMENTED` |
| DOC-02 | `docs/sprint-2/architecture.md` | Ausente | `NOT_STARTED` |
| DOC-03 | `docs/sprint-2/endpoints.md` | Ausente | `NOT_STARTED` |
| DOC-04 | `docs/sprint-2/data-model.md` | Ausente | `NOT_STARTED` |
| DOC-05 | `docs/sprint-2/observability.md` | Ausente | `NOT_STARTED` |
| DOC-06 | `docs/sprint-2/ai-integration.md` | Ausente | `NOT_STARTED` |
| DOC-07 | `docs/sprint-2/demo-script.md` | Ausente | `NOT_STARTED` |
| DOC-08 | `docs/sprint-2/deployment.md` | Ausente | `NOT_STARTED` |
| DOC-09 | `docs/sprint-2/delivery-checklist.md` | Ausente | `NOT_STARTED` |
| DOC-10 | `README.md` atualizado para Mongo, Docker, demo e APK | Descreve SQL Server | `PARTIAL` |

---

### 4.15 DELIVERABLES

| ID | Requisito | Estado | Status |
|---|---|---|---|
| DEL-01 | `dist/backend/` | Ausente | `NOT_STARTED` |
| DEL-02 | `dist/mobile/` | Ausente | `NOT_STARTED` |
| DEL-03 | `dist/presentation-assets/` | Ausente | `NOT_STARTED` |
| DEL-04 | Export de `openapi.json` no pipeline | Ausente | `NOT_STARTED` |
| DEL-05 | Dockerfile, compose com replica set e deploy Dokploy/Traefik | Ausente | `NOT_STARTED` |

---

## 5. Riscos de regressão identificados

| # | Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|---|
| R1 | Perda de atomicidade entre agregado, `AuditLog` e `ProjectSnapshot` ao trocar o change tracking do EF por escrita explícita. | Alta | Crítico | `IUnitOfWork` transacional e teste de rollback com falha injetada. |
| R2 | Stores de Identity escritos à mão divergirem do contrato e quebrarem login e papéis. | Média | Crítico | Implementar só os contratos usados; testes de integração contra Mongo real. |
| R3 | Handlers dependerem implicitamente do change tracking do EF (mutação sem `Update`). | Alta | Alto | Revisão handler a handler; repositórios com `Update` explícito. |
| R4 | Testes de integração hoje acoplados ao EF InMemory. | Certa | Alto | Migrar a factory para Testcontainers com replica set. |
| R5 | Conflito de peer dependency dos gráficos no Expo 54 (achado A14). | Alta | Médio | **Resolvido.** Spike executado: `victory-native` 42.x exige Skia >= 2.6.0 e o SDK 54 fixa 2.2.12; 41.19.3 aceita. Reanimated 4 exige `react-native-worklets`. `expo-doctor` 18/18. |
| R6 | Indisponibilidade do Gemini derrubar o fluxo principal. | Média | Alto | FlowScore independente do modelo; timeout, circuit breaker e degradação previsível. |
| R7 | Ausência de credenciais OneSignal e EAS impedir a validação live. | Alta | Médio | Implementar e testar por contrato, marcando a validação live como pendente, sem simular sucesso. |
| R8 | Docker parado no ambiente bloquear testes e imagem (achado A13). | **Materializou-se** | Alto | **Contornado, não resolvido.** O Docker Desktop desta máquina não sobe. Os testes de integração rodam contra um MongoDB 8.0.30 real em replica set instalado localmente, via `FLOW_TEST_MONGO_URI`; o fixture cai em Testcontainers onde houver daemon. O **build da imagem permanece não executado** — ver seção 9. |
| R9 | Semântica de `DateTimeOffset` no Mongo (serialização e comparação). | Média | Alto | Serializer explícito e testes de round-trip. |
| R10 | `decimal` no Mongo exigir `Decimal128` para não perder precisão financeira. | Alta | Alto | Representação explícita e testes de precisão de ROI. |

---

## 6. Evidências da Fase 1 — migração para MongoDB

Executado em 09/09/2026 contra MongoDB 8.0.30 em replica set `rs0` de nó único.

### 6.1 Resultado dos testes

```text
Flow.Domain.Tests          124 testes  0 falhas
Flow.Application.Tests      10 testes  0 falhas
Flow.Integration.Tests      54 testes  0 falhas   (MongoDB real)
--------------------------------------------------
Total                      188 testes  0 falhas
dotnet build Flow.sln       0 erros    0 avisos
```

O baseline tinha 121 testes contra EF InMemory. Os 54 testes de integração agora rodam
contra um MongoDB real e descartável, com transações reais.

### 6.2 Requisitos que passam a `VERIFIED`

| ID | Evidência |
|---|---|
| DB-01 | Nenhuma referência a EF Core em `src/`. `ApplicationDbContext`, configurations e migrations removidos. |
| DB-02 | `MongoDB.Driver` 3.11.1, `MongoClient` singleton em `Flow.Infrastructure/DependencyInjection.cs`. |
| DB-03 | `docs/sprint-2/data-model.md` escrito antes do código, a partir dos access patterns. |
| DB-04 | `MongoIndexInitializer` cria 41 índices de forma idempotente no startup. |
| DB-05 | `TransactionalIntegrityTests` exercita transação multi-documento real. |
| DB-06 | `IUnitOfWork.ExecuteAsync` na Application; `IClientSessionHandle` não aparece em nenhuma assinatura da camada. |
| DB-07 | `IAuditLogRepository` e `IProjectSnapshotRepository` só expõem append e leitura. |
| AUTH-01..05 | `AuthTests` cobre registro, login, papéis, 401 e a matriz 403 por perfil. |
| AUTH-06 | `Refresh_ReusingARotatedToken_IsRejectedAndKillsTheChain` e `RefreshTokens_AreStoredOnlyAsHashes`. |
| AUTH-07 | `DemoDataSeeder` idempotente sob `SEED_DEMO_DATA`; segunda execução não duplica. |
| AUTH-08 | `MongoUserStore` e `MongoRoleStore` sobre os contratos oficiais do Identity, sem pacote comunitário. |
| STR-01..09 | `GuidelinesController` com CRUD, `current`, histórico e filtros; `StrategicGuidelineTests` cobre a vigência derivada. |
| IDEA-01..10 | `InnovationPipelineTests` cobre draft, edição, exclusão, submissão, score, FlowScore, comparação e autorização por recurso. |
| PRJ-01..07 | `ProjectAndResultTests` cobre a máquina de estados, stage, progresso, risco e snapshots. |
| RES-01..06 | Estimated e Actual independentes, ROI, produtividade, horas e qualidade, com precisão `Decimal128` verificada. |
| DASH-01..06 | `DashboardTests` cobre base vazia e base realista; `DashboardCompositionTests` cobre a aritmética de borda. |
| SEC-01, SEC-02 | ProblemDetails com `traceId`; `ValidationBehavior` ativo devolvendo 422. |
| SEC-03 | Autorização por recurso verificada em `Idea_OfAnotherOperator_IsNotReadable`. **Revisado na seção 13:** a proteção existia só nesse endpoint, e os vizinhos não a tinham. |
| SEC-08 | Rotação, revogação e hash do refresh token verificados. **Revisado na seção 13:** o consumo não era atômico sob concorrência. |
| OBS-04 | `/health/live` e `/health/ready` respondendo, com o Mongo como dependência de readiness. |

### 6.3 Defeitos encontrados e corrigidos durante a fase

Registrados porque são exatamente o tipo de regressão que uma migração introduz em silêncio.

| # | Defeito | Como apareceu | Correção |
|---|---|---|---|
| D1 | `MapIdMember` falhava para entidades que herdam `Id` de `BaseEntity`. | API não subia. | Registrar o class map de `BaseEntity`; a convenção do driver resolve o id por herança. |
| D2 | `UserManager` normaliza o nome do papel antes de chegar ao store, então `LEADERSHIP` era persistido. `ClaimsPrincipal.IsInRole` compara valor com ordinal sensível a caso, e **todo** `[Authorize(Roles = ...)]` devolvia 403. | Login funcionava, mas `GET /dashboard/summary` devolvia 403 para Leadership. | O store resolve o nome normalizado para o nome canônico do papel antes de gravar. |
| D3 | `["completed"] = 0` em projeção de inclusão era interpretado como exclusão pelo MongoDB. | `GET /dashboard/summary` devolvia 500. | Envolver em `$literal`. |
| D4 | O seeder só retrodatava `createdAt`, então tempo médio de conclusão e dias bloqueado ficavam em zero. | Dashboard com KPI zerado apesar de dados populados. | Retrodatar também `startDate`, `completedAt`, `blockedSince` e distribuir auditoria e snapshots ao longo do período. |
| D5 | Nome de banco de teste com 75 caracteres excedia o limite de 63 do MongoDB. | Toda a suíte de integração falhava na criação de índices. | Encurtar os identificadores gerados. |

### 6.4 Mudanças de contrato HTTP

| Situação | Antes | Agora | Motivo |
|---|---|---|---|
| Falha de validação | 400 | **422** | Distinguir entrada malformada de conflito de estado, como o brief pede para o tratamento no mobile. |
| `DomainException` | 400 | **409** | Depois da validação de entrada, o que resta são transições inválidas, que são conflito de estado. |
| Erros | sem `traceId` | `traceId` no ProblemDetails | Liga o erro reportado ao log, ao trace e ao `correlationId` da auditoria. |
| Credencial errada, refresh revogado ou replay | 403 | **401** | `401` é "não sei quem você é", `403` é "sei, e não pode". O app reage diferente a cada um; colapsar os dois faz uma senha errada parecer falta de permissão. Corrigido na Fase 4. |
| Falha de binding (JSON malformado, tipo errado) | 400, em outro formato, sem `traceId` | **422**, no mesmo ProblemDetails | Havia dois caminhos de validação com dois contratos. As `DataAnnotations` dos comandos foram removidas e o FluentValidation ficou como autoridade única. Corrigido na Fase 4. |

---

## 7. Evidências da Fase 2 — inteligência e notificações

### 7.1 Requisitos que passam a `VERIFIED`

| ID | Evidência |
|---|---|
| AI-01 | `Flow.Application/Assistant/AssistantContracts.cs`. A Application declara `AssistantResult<T>`, `AssistantOutcomeKind` e as três operações. Não há tipo do `Google.GenAI` fora de `Flow.Infrastructure`. |
| AI-02 | `CompareIdeas_ReturnsStructuredInsightAndRecordsAGovernanceRun`. O copiloto devolve avaliação por ideia, riscos, trade-offs e recomendação, tipados. |
| AI-04 | `ProjectDraft_CreatesNothingUntilAHumanConfirmsIt`. Depois da chamada, a contagem de projetos é a mesma. O rascunho só vira projeto pelo comando normal, com auditoria e snapshot. |
| AI-05 | `ExecutiveInsights_AreBuiltOnlyFromTheDashboardPayload`. |
| AI-06 | O schema exige `evidenceWasSufficient`; com base pobre o serviço declara evidência insuficiente em vez de produzir análise. |
| AI-07 | `ResilienceTests` cobre o circuit breaker em 5 cenários; timeout por `HttpOptions`; **sem retry** — content generation é POST metered e não idempotente. |
| AI-08 | `flow_ai_requests`, `flow_ai_failures` e `flow_ai_latency_ms`, com label de operação. Nenhuma chave, JWT ou conteúdo sensível é registrado. |
| AI-09 | `AssistantRunRecorder` grava em `assistant_runs` **inclusive as falhas**: solicitante, papel, operação, modelo, desfecho, latência, tokens e `correlationId`. |
| AI-10 | `Gemini:ApiKey` só é lido em `Flow.Infrastructure`. Não existe no `app.config.ts` nem em nenhum perfil do `eas.json`. |
| EXT-01 | `OneSignalPushSender` sobre `POST https://api.onesignal.com/notifications` com `include_aliases.external_id`. Validação live pendente de credencial — ver seção 9. |
| EXT-02 | Coleção `notifications` com leitura, marcação e deep link; 3 endpoints. |
| EXT-03 | `notification_outbox` gravado na mesma transação do domínio; `OutboxDispatcherHostedService` despacha fora dela. |
| EXT-04 | `ATransientFailureSchedulesARetryInsteadOfLosingTheMessage`, `RepeatedTransientFailuresEventuallyDeadLetter`, `APermanentFailureIsNotRetried` e `DispatchIsIdempotentAcrossDrains`. **Revisado na seção 13:** valia para um dispatcher, não para duas réplicas. |
| EXT-05 | Timeout e cancelamento propagados; breaker próprio para o Gemini; desfecho `NotConfigured` tratado como estado válido. |

### 7.2 Decisão registrada: context injection em vez de function calling

**AI-03** pedia function calling sobre dados autorizados. A implementação injeta o contexto
já filtrado pela autorização do usuário, e a decisão está registrada em
[`ai-integration.md`](ai-integration.md).

O motivo é de segurança, não de esforço: com function calling o modelo escolhe **quais**
dados buscar, e a fronteira de autorização passa a depender do que ele decidir chamar. Com
injeção, a consulta acontece antes, sob a identidade do usuário, e o modelo recebe apenas o
que aquele usuário já poderia ler. As três operações do produto têm escopo fechado e
conhecido de antemão, então function calling não acrescentaria capacidade — só superfície.

`Copilot_IsNotAvailableToOperators`, `ExecutiveInsights_AreLeadershipOnly` e
`ProjectDraft_ForAnUnapprovedIdea_IsRefusedBeforeCallingTheProvider` verificam que a
autorização é aplicada **antes** de qualquer chamada ao provedor.

**Status de AI-03:** `VERIFIED` como decisão de arquitetura documentada e testada, não como
function calling literal.

### 7.3 Comportamento sem credencial

`WhenTheProviderFails_TheApiDegradesAndStillRecordsTheAttempt` e
`WhenTheAssistantIsDown_TheRestOfTheProductKeepsWorking` verificam que:

- os endpoints inteligentes respondem `503` com ProblemDetails, nunca `500`;
- a tentativa é registrada em `assistant_runs` mesmo tendo falhado;
- ideias, projetos, dashboard e FlowScore continuam funcionando;
- `WithoutCredentials_MessagesStayPendingRatherThanBeingFakedAsSent` garante que **nada é
  marcado como entregue** sem credencial de push.

---

## 8. Evidências da Fase 3 — aplicativo mobile

### 8.1 Requisitos que passam a `VERIFIED`

| ID | Evidência |
|---|---|
| MOB-01 | `app.config.ts` + `src/config/env.ts`. Em desenvolvimento a URL é derivada do host que serve o bundle; `EXPO_PUBLIC_API_URL` sobrepõe. |
| MOB-02 | `src/api/client.ts`. Single-flight por promessa compartilhada: 401 concorrentes disparam **um** refresh; a falha limpa a sessão e leva ao login. |
| MOB-03 | 4 telas de operador: home pela estratégia vigente, formulário que começa pelo problema, minhas ideias com filtro e detalhe com retorno e pontos. |
| MOB-04 | 10 telas de gestor: fila ordenada por FlowScore, avaliação, comparação, copiloto, revisão de rascunho, projetos, detalhe, progresso e etapa, linha do tempo e resultado. |
| MOB-05 | 2 telas de liderança: painel executivo com gráficos e insights. |
| MOB-06 | 6 telas compartilhadas: avisos, perfil, estratégias, detalhe, formulário e histórico. |
| MOB-07 | `victory-native` 41.19.3 + Skia 2.2.12 + Reanimated 4.1.1 + `react-native-worklets` 0.5.1. Spike documentado no risco R5. |
| MOB-08 | `components/QueryView.tsx` centraliza loading, vazio e erro com retry, sobre o estado do TanStack Query. |
| MOB-09 | `i18n/labels.ts`; toda a interface em pt-BR, incluindo enums de domínio, datas e moeda. |
| MOB-10 | `src/api/errors.ts` tipa ProblemDetails; 422 vira erro de campo, 409 vira conflito de estado, 401 dispara refresh, 503 vira degradação anunciada. |
| APK-01 | `eas.json` com 4 perfis; `preview` e `production` produzem **APK**. |
| APK-02 | `npx tsc --noEmit` sem saída; `npx expo-doctor` **18/18**. |

### 8.2 Verificação executada

```text
npx tsc --noEmit                    sem saída
npx expo-doctor                     18/18 checks
npx expo export --platform android  bundle Hermes de 5,59 MB
```

23 telas, cliente REST tipado, nenhum mock e nenhum dado falso. O aplicativo fala apenas
com a API.

### 8.3 Defeitos corrigidos na fase

| # | Defeito | Correção |
|---|---|---|
| D6 | `expo-font` duplicado: `@expo/vector-icons` 15.1.1 arrastava a 57 enquanto o SDK 54 fixa 14.0.12. | `npx expo install expo-font`, alinhando com o `bundledNativeModules` do SDK. |
| D7 | Reanimated 4 exige `react-native-worklets` como peer, ausente. | Adicionado 0.5.1. |
| D8 | `usesCleartextTraffic` não é campo tipado de `android` no `app.config.ts`. | Plugin `expo-build-properties`. |

---

## 9. Evidências da Fase 4 — segurança, observabilidade e entregáveis

### 9.1 Requisitos que passam a `VERIFIED`

| ID | Evidência |
|---|---|
| SEC-04 | `AddCors` com origens explícitas por `CORS_ALLOWED_ORIGINS`. Sem curinga. |
| SEC-05 | Rate limiting nativo do .NET 8, particionado, com política mais restrita em auth e nos endpoints de IA. `RepeatedLoginAttempts_AreEventuallyRefusedWith429`. |
| SEC-06 | Nenhum segredo em `appsettings.json`; `.env.example` só com placeholders; `.env` no `.gitignore`. |
| SEC-07 | A API **recusa iniciar** fora de Development com placeholder ou com segredo menor que 32 bytes. |
| SEC-09 | Nenhum log de chave, JWT, senha ou corpo de requisição. O que é e o que não é registrado está em [`observability.md`](observability.md). |
| SEC-01 (revisto) | Contrato de erro único: 401 para autenticação, 403 para autorização, 422 para entrada — inclusive falha de binding —, 409 para conflito, 503 para dependência degradada. Sem `400` no contrato. |
| MOB-09 (revisto) | Nenhum texto em inglês chega à interface: mensagens de validação localizadas no servidor com rótulo de campo em pt-BR, e o cliente com a própria cópia por tipo de erro. `ValidationMessages_ComeBackInPortuguese` verifica as duas metades. |
| OBS-01 | Serilog estruturado com enriquecimento e `UseSerilogRequestLogging`; `/health` cai para Verbose para não poluir. |
| OBS-02 | Instrumentação de ASP.NET Core, HttpClient e MongoDB. |
| OBS-03 | Métricas de runtime + 11 de negócio. |
| OBS-05 | `traceId` no ProblemDetails, no log e em `audit_logs.correlationId` — o mesmo identificador. |
| OBS-06 | `flow_ideas_submitted`, `flow_ideas_approved`, `flow_ideas_rejected`, `flow_projects_created`, `flow_projects_blocked`, `flow_projects_completed`, `flow_ai_requests`, `flow_ai_failures`, `flow_ai_latency_ms`, `flow_notifications_sent`, `flow_notifications_failed`. Labels de baixa cardinalidade, sem id de usuário nem de recurso. |
| OBS-07 | Sem `OpenTelemetry:OtlpEndpoint` a instrumentação segue ativa em processo e a API sobe normalmente. |
| DOC-01..09 | Os nove documentos existem em `docs/sprint-2/`. |
| DOC-10 | `README.md` reescrito: visão, arquitetura, stack, requisitos, configuração, MongoDB, replica set, variáveis, backend, mobile, execução local, Docker, testes, demo, APK e deploy. |
| DEL-01 | `dist/backend/` com fonte, testes, solução, Dockerfile, compose, `.env.example` e documentação, sem `bin`, `obj` nem settings locais. |
| DEL-02 | `dist/mobile/flow-mobile/` sem `node_modules`, reprodutível pelo lockfile. |
| DEL-03 | `dist/presentation-assets/` com `openapi.json`, `endpoints.txt`, `test-results.txt` e os nove documentos. |
| DEL-04 | `scripts/build-artifacts.sh` exporta o `openapi.json` **da aplicação em execução**, não de uma cópia mantida à mão. |

### 9.2 Defeito de fase encontrado ao escrever a documentação

| # | Defeito | Como apareceu | Correção |
|---|---|---|---|
| D9 | As métricas de negócio estavam **definidas e nunca incrementadas**. | Ao redigir `observability.md` para afirmar que existiam. Afirmar seria falso. | `IFlowMetrics` na Application com `NullFlowMetrics` de fallback, ligado aos handlers de submissão, aprovação e rejeição, ao `ProjectTransitionRecorder` (**após** o commit, para não contar transição que sofreu rollback), ao `AssistantRunRecorder` e ao dispatcher do outbox. |
| D10 | O rate limiting lia a configuração de `builder.Configuration` **antes** do `Build()`, então a sobreposição do host de teste nunca era aplicada — um controle que ignora a própria configuração em silêncio. | O teste de 429 não conseguia ligar o limite. | Resolver `IOptionsMonitor<RateLimitOptions>` por requisição a partir de `http.RequestServices`, com `RateLimitPartition.GetNoLimiter` quando desligado. |
| D11 | Colisão de schema id no Swagger entre `IdeasController+CompareIdeasRequest` e `AssistantController+CompareIdeasRequest`. | `SwaggerGeneratorException`: **nenhuma** especificação era gerada. | `SchemaIdFor` prefixa tipos aninhados com o nome do tipo declarante. |
| D12 | Falha de autenticação devolvia **403**. | Revisão do contrato HTTP ao documentar os códigos. | `UnauthorizedException` mapeada para 401; login e refresh passaram a usá-la. |
| D13 | `[Required]` nos comandos fazia o `[ApiController]` recusar com **400**, em outro formato e sem `traceId`, antes do FluentValidation rodar. | O teste de mensagem em pt-BR recebeu 400 onde o contrato documenta 422. | `DataAnnotations` removidas dos 7 comandos; `InvalidModelStateResponseFactory` devolve 422 no formato da casa, então falha de binding também carrega `traceId`. |
| D14 | Mensagens de validação em inglês em uma interface pt-BR — e o cliente ainda ecoava o `title` da exceção, também em inglês. | Revisão do fluxo de erro ponta a ponta. | Cultura do FluentValidation em pt-BR, rótulos de campo em `FieldLabels`, e o app deixou de ecoar `title`: usa a própria cópia por tipo de erro, e só exibe texto do servidor quando ele vem em `userMessage`. |

### 9.3 Verificação executada

```text
dotnet build Flow.sln -c Release               0 erros, 0 avisos
dotnet test  Flow.sln                        273 testes, 0 falhas
./scripts/build-artifacts.sh                 dist/ gerado
openapi.json                                  54 endpoints, 73 schemas, exportado da aplicação
npx tsc --noEmit                              sem saída
npx expo-doctor                               18/18
```

### 9.4 Fumaça ponta a ponta contra a aplicação em Release

Executada com a API publicada em Release, contra o MongoDB 8.0.30 em replica set, com o
dataset de demonstração semeado. Não é o mesmo que a suíte: aqui é o binário de entrega
respondendo a HTTP de verdade.

| Verificação | Resultado |
|---|---|
| `GET /health/live` | `Healthy` |
| `GET /health/ready` | `Healthy`, com o check `mongodb` em ~42 ms |
| Seed de demonstração | 4 usuários, 10 ideias, 6 projetos, idempotente |
| Login da liderança | 200, token emitido |
| `GET /dashboard/summary` | 200 com KPI real: 80% de aprovação, 100% de conversão, 91,5 dias de conclusão média, 23 dias bloqueado, índice de gargalo 50 |
| Senha errada | **401** |
| Operador em endpoint de liderança | **403** |
| Título vazio | **422**, `errors.Title` = `'título' deve ser informado.`, com `traceId` |
| `POST /dashboard/insights` sem chave | **503** com `userMessage` em pt-BR, e o resto da API intacto |
| Acentuação ponta a ponta | Gravada e lida sem perda: `Manutenção preditiva na célula de solda` |

---

## 10. Estado final por área

| Área | Requisitos | `VERIFIED` | Observação |
|---|---|---|---|
| AUTH | 8 | 8 | — |
| STRATEGY | 9 | 9 | — |
| IDEAS | 10 | 10 | — |
| PROJECTS | 7 | 7 | — |
| RESULTS | 6 | 6 | — |
| DASHBOARD | 6 | 6 | — |
| DATABASE | 7 | 7 | — |
| MOBILE_INTEGRATION | 10 | 10 | — |
| EXTERNAL_SERVICE | 5 | 5 | EXT-01 verificado por contrato; entrega live pende de credencial |
| OBSERVABILITY | 7 | 7 | Sem coletor OTLP neste ambiente; a API sobe e instrumenta assim mesmo |
| SECURITY | 9 | 9 | — |
| AI_PLUS | 10 | 10 | AI-03 fechado como decisão documentada (seção 7.2); chamada live pende de chave |
| APK | 4 | **2** | APK-03 e APK-04 pendem de credencial EAS |
| DOCUMENTATION | 10 | 10 | — |
| DELIVERABLES | 5 | **4** | DEL-05 escrito e revisado; imagem não construída nesta máquina |
| **Total** | **113** | **110** | **3 pendentes, todos por credencial ou ambiente externo** |

---

## 11. Pendências reais

Nenhuma delas depende de código que falte escrever.

| ID | Pendência | Bloqueio | Como fechar |
|---|---|---|---|
| APK-03 | Build do APK | Credencial EAS com assinatura Android | `cd mobile && eas build --platform android --profile preview` |
| APK-04 | Instalação em aparelho | Depende de APK-03 | Instalar o `.apk` e percorrer o roteiro de demonstração |
| DEL-05 | Deploy com HTTPS no Dokploy | Sem credencial Dokploy | Publicar e associar o domínio. **A imagem e o compose passaram a ser construídos e executados na CI**, então o que resta é só o deploy. |

### Itens que dependem só de chave, já implementados e testados por contrato

| Item | O que falta | Comportamento hoje |
|---|---|---|
| Chamada real ao `gemini-3.8-flash` | `Gemini__ApiKey` | Endpoints inteligentes respondem `503`; o resto funciona igual |
| Entrega real de push | Credencial OneSignal/FCM | Central de avisos funciona; o outbox mantém as mensagens **pendentes**, sem fingir entrega |
| Visualização em coletor OTLP | Um coletor | Instrumentação ativa em processo; a API sobe normalmente |

> Nenhum destes foi marcado como sucesso. O produto trata a ausência de credencial como um
> estado previsto, e os testes verificam justamente esse estado.

---

## 13. Rodada de revisão técnica independente

Uma revisão da branch encontrou cinco problemas, todos confirmados no código e depois
provados por teste antes de qualquer correção. O método foi sempre o mesmo: escrever o
teste, rodá-lo contra o código publicado, ver a falha, corrigir, ver o teste passar.

### 13.1 O que estava errado

| # | Problema | Causa raiz | Evidência antes da correção |
|---|---|---|---|
| D15 | IDOR nos endpoints de leitura | A checagem por recurso estava escrita à mão dentro de um único handler, então os vizinhos nasceram sem ela | 5 testes falharam devolvendo **200** onde precisava ser 403: projeto, linha do tempo, **resultado financeiro**, comentários de ideia alheia, e a listagem enumerando projeto de outro operador |
| D16 | Rotação de refresh token não atômica | Check-then-act: ler, conferir `IsActive` em memória, gravar por `_id` sem condição de estado | O perdedor recebia **500** (write conflict cru) e, depois da corrida, o token do vencedor **ainda funcionava** — sobrava uma segunda cadeia viva |
| D17 | Chave de idempotência inválida no OneSignal | `DedupeKey` interno (`IdeaApproved:{id}:{id}`) enviado no campo que a API exige em UUID RFC 9562 | 4 testes falharam: a chave não era UUID, vazava para o provedor, e retentativas da mesma mensagem não reusavam a chave |
| D18 | Falso positivo de entrega | Todo 2xx virava `Delivered` | 2 testes falharam: 200 sem `id` era marcado como entregue, quando a documentação diz que sem `id` **nada foi criado** |
| D19 | Outbox inseguro entre réplicas | `GetDueAsync` seguido de `UpdateAsync`: janela entre ler e marcar | 12 mensagens e 4 dispatchers concorrentes produziram **28 entregas** — 16 notificações duplicadas |
| D20 | Rate limit de IA por endereço, não por usuário | `UseRateLimiter` antes de `UseAuthentication` **e** leitura da claim `sub`, que o handler de bearer mapeia para `NameIdentifier` | O segundo usuário autenticado recebia **429** sem ter gasto nada |

### 13.2 Uma premissa que estava incompleta

A revisão atribuía o problema do rate limit apenas à ordem do middleware. Está certo, mas
não basta: com a ordem já corrigida e a leitura da claim intacta, o teste **continua
falhando**. O handler de bearer mapeia `sub` para `ClaimTypes.NameIdentifier` por padrão,
então procurar só por `sub` devolve vazio mesmo em requisição autenticada, e a partição cai
no endereço de novo. Eram dois defeitos independentes; corrigir um só teria deixado o bug
de pé com a aparência de resolvido.

Também vale registrar o que **não** foi feito. A instrução conservadora era restringir
projetos e resultados a Manager e Leadership caso a especificação não exigisse acesso do
Operator. A especificação exige: a matriz de papéis em
`docs/specs/2026-05-13-flow-mvp-design.md` diz `GET /projects` → "Operator (own)". Então
implementei o "own" de forma explícita — o operador acessa o projeto quando é o responsável
ou quando o projeto veio de uma ideia que ele enviou — em vez de fechar o acesso e
contrariar a especificação.

### 13.3 O que passou a existir

| Área | Mudança |
|---|---|
| AUTHZ | `ResourceAccessPolicy` na Application, um lugar só para a regra; listagem restringida **na consulta**, não filtrada depois |
| AUTH | `TryConsumeAsync` e `TryRevokeAsync` com compare-and-set; emissão do substituto dentro da mesma transação, que aborta se a corrida for perdida |
| PUSH | `DeliveryId` (o `_id` do outbox) como `idempotency_key`; resposta do provedor interpretada em vez de presumida |
| OUTBOX | Estado `Processing` com lease, claim atômico por `findOneAndUpdate`, retomada de lease vencido, índice `ix_outbox_lease` |
| API | Pipeline reordenado; `UserIdentity` como leitura única do id do usuário |

### 13.4 Testes acrescentados

| Arquivo | Testes | Cobre |
|---|---|---|
| `ResourceAuthorizationTests` | 15 | IDOR em ideia, comentários, projeto, linha do tempo, snapshots, resultado e listagem; caminho positivo do operador; acesso integral de Manager e Leadership; anônimo em 401 |
| `RefreshTokenConcurrencyTests` | 8 | Doze consumos simultâneos com um vencedor só; a corrida por HTTP em cinco rodadas conferindo o banco; cadeia morrendo; rotação sequencial; logout não revogando token alheio |
| `OneSignalSenderTests` | 20 | Chave de idempotência, reuso em retentativa, chaves distintas, entrega comprovada, falso positivo, classificação de falha, chave da API fora do corpo |
| `OutboxConcurrencyTests` | 9 | Duas e quatro réplicas concorrentes, lease vivo e vencido, retomada, transitório, permanente, dead-letter, sem credencial |
| `RateLimitPartitioningTests` | 8 | Cotas separadas por usuário no mesmo endereço, mesmo usuário ainda limitado, dois aparelhos dividindo cota, `/auth/*` por endereço, 401 e 403 intactos |

**60 testes novos.** Suíte: **273** (124 Domain + 10 Application + 139 Integration), 0 falhas,
0 ignorados. Build em Release sem avisos.

### 13.5 Fumaça contra o binário publicado

18 verificações contra a API em Release com o dataset de demonstração: 403 em cada porta de
recurso alheio, 200 na própria trilha, 401 anônimo, rotação e reuso de refresh token,
degradação do assistente com `userMessage` em pt-BR, e o painel executivo intacto.

---

## 14. Segunda rodada de hardening

Cinco problemas de uma segunda revisão independente, mais duas melhorias de infraestrutura.
Mesmo método: escrever o teste, rodar contra o código publicado, ver falhar, corrigir.

### 14.1 O que estava errado

| # | Problema | Causa raiz | Evidência antes da correção |
|---|---|---|---|
| D21 | Erro transitório do Mongo lido como reuso de token | `TryConsumeAsync` capturava `TransientTransactionError` e devolvia `false`, que o handler interpreta como token já consumido | Com o erro nascendo onde o `catch` vivia, uma **primeira rotação legítima voltou 401** e a cadeia foi revogada — um step-down deslogaria o usuário de todas as sessões |
| D22 | Transação sem semântica de retry | `StartTransaction`/`CommitTransactionAsync` na mão, sem retry da transação em `TransientTransactionError` nem retry só do commit em `UnknownTransactionCommitResult` | 5 de 7 testes falharam com **500**: nenhuma retentativa acontecia |
| D23 | Forwarded headers não processados | Pipeline sem `UseForwardedHeaders`, e o padrão do framework só confia em loopback | O app via **o endereço do proxy para todo mundo**; um segundo cliente atrás do mesmo Traefik recebeu **429** por algo que nunca fez, e `X-Forwarded-Proto: https` não movia `Request.Scheme` |
| D24 | `errors` do OneSignal modelado como `List<string>` | O campo é polimórfico: array quando nada foi criado, objeto quando a mensagem **foi** criada e alguns destinatários foram pulados | `{"id":"uuid","errors":{...}}` fazia o parse lançar, o corpo virava "ilegível" e uma **mensagem criada era classificada como falha transitória** e reagendada |
| D25 | Conclusão do outbox sem fencing | A escrita pós-claim era endereçada só pelo `_id` | A escrita do worker com lease vencido foi **aceita**, e um `MarkFailed` obsoleto sobrescreveu o `Dispatched` de quem entregou |

### 14.2 Decisões de implementação

**Transações.** Migrei `MongoUnitOfWork` para `WithTransactionAsync`, do próprio driver, em
vez de reimplementar os dois laços de retentativa. A API oficial já implementa as duas
regras, elas são sutis, e o fornecedor mantém. O callback pode rodar mais de uma vez — é o
sentido da regra transitória — e isso é seguro porque tudo lá dentro escreve pela sessão;
o que precisa acontecer uma vez só já estava fora da transação. Declarei read e write
concern `majority` explicitamente: a auditoria é a evidência do produto, e um commit
reconhecido por uma minoria some numa eleição.

`TryConsumeAsync` deixou de capturar qualquer erro. `false` significa uma coisa só: nenhum
documento correspondeu. Falha de infraestrutura sobe como falha de infraestrutura.

**Proxy.** A configuração é uma lista de proxies e redes confiáveis, não um interruptor.
Limpar `KnownProxies` e `KnownNetworks` faria funcionar e transformaria um cabeçalho
controlado pelo cliente na identidade do cliente — qualquer um escolheria o próprio
endereço para escapar do rate limit. A API **recusa iniciar** fora de Development se a
opção estiver ligada sem nada confiável declarado.

**Fencing.** Cada claim cunha um `leaseToken` novo, e toda mutação pós-claim exige
`_id` + `Processing` + esse token. O token é por claim e não por worker porque o mesmo
worker pode ter dois claims da mesma mensagem em momentos diferentes, e uma escrita do
primeiro casaria pelo nome. Como transação Mongo e requisição HTTP externa não podem ser
atômicas juntas, a defesa continua em duas camadas: o fencing impede a sobrescrita, a chave
de idempotência impede a duplicata.

### 14.3 O que não deu para testar como pedido

`UnknownTransactionCommitResult` pede que o commit seja retentado sem reexecutar a
operação. Isso agora é implementação do driver, não nossa — é justamente por isso que
migrei para a API oficial. Não escrevi teste para ele: exigiria interceptar o commit por
dentro da sessão, e o que estaria sendo testado seria o MongoDB, não o Flow. O que testei é
o que controlo: erro transitório reexecutando o callback e comitando o trabalho **uma vez
só**.

### 14.4 Testes acrescentados

| Arquivo | Testes | Cobre |
|---|---|---|
| `TransactionRetrySemanticsTests` | 7 | Transitório retentado sem virar reuso; transação retentada comitando uma vez; três transitórios absorvidos; falha não transitória em 500 sem revogar; CAS perdido ainda revogando; o contraste lado a lado; retry de um UnitOfWork qualquer |
| `ForwardedHeadersTests` | 13 | Proxy confiável reportando endereço e esquema; proxy por endereço exato; cliente direto não forjando nada; feature desligada; cadeia de saltos; baldes separados por cliente; IA ainda por usuário; 401 e 403 intactos; configuração recusando CIDR inválido e recusando ficar ligada sem ninguém confiável |
| `OneSignalSenderTests` | +4 | `id` válido com `errors` objeto ainda sendo entrega; objeto sem `id` não quebrando o parse; resposta sem campo `id`; resumo citando chave e contagem sem citar identificadores |
| `OutboxLeaseFencingTests` | 7 | Worker sem lease não sobrescrevendo quem assumiu; mesmo worker não reusando token antigo; token já limpo recusado; conclusão, falha e liberação com lease válido; chave de idempotência sobrevivendo à retomada |
| `LayerBoundaryTests` | 9 | Domain sem Application/Infrastructure/API; Application sem Infrastructure/API; nenhum dos dois com o driver do Mongo; nenhuma assinatura da Application com `IClientSessionHandle`, inclusive dentro de genéricos; e a asserção espelho de que a Infrastructure **tem** o driver |

**40 testes novos.** Suíte: **313** (124 Domain + 10 Application + 9 Architecture + 170
Integration), 0 falhas, 0 ignorados. Build em Release sem avisos.

### 14.5 CI

`.github/workflows/ci.yml`, com `permissions: contents: read`, sem `pull_request_target` e
sem segredo. Quatro jobs: backend com a suíte contra MongoDB real e um passo que falha se
algum teste for pulado; mobile com typecheck, doctor e export Android; **Docker construindo
a imagem e subindo o compose**, que é a primeira validação real do `Dockerfile`; e
artefatos conferindo o `openapi.json` exportado da aplicação.

---

## 12. Histórico de atualização

| Data | Fase | Alteração |
|---|---|---|
| 09/09/2026 | Fase 0 | Auditoria inicial, baseline, pesquisa de versões e criação da matriz. |
| 09/09/2026 | Fase 1 | Migração integral para MongoDB, Identity sobre Mongo, expansão de domínio, dashboard agregado, seed de demonstração e suíte de 188 testes. |
| 09/09/2026 | Fase 2 | Copiloto do gestor, rascunho de projeto, insights executivos, governança em `assistant_runs`, central de notificações, outbox com retry e dead-letter. |
| 09/09/2026 | Fase 3 | Aplicativo mobile reconstruído: 23 telas em pt-BR, cliente tipado, refresh single-flight, gráficos, `expo-doctor` 18/18. |
| 09/09/2026 | Fase 4 | CORS, rate limiting, Serilog, OpenTelemetry, health checks, métricas de negócio ligadas de fato, Docker, compose, artefatos, README e os nove documentos. Suíte final de **213 testes**. |
| 09/09/2026 | Revisão | Cinco correções de segurança e concorrência (seção 13): IDOR, rotação atômica de refresh token, idempotência do push, claim atômico do outbox e rate limit por usuário. 60 testes novos; suíte de **273**. |
| 10/09/2026 | Hardening 2 | Semântica de retry das transações, forwarded headers atrás do Traefik, contrato real do OneSignal, fencing de lease no outbox, guardas de arquitetura e CI no GitHub Actions. 40 testes novos; suíte de **313**. |
