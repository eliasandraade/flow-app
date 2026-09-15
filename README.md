# Flow

Plataforma de **gestão do ciclo de vida da inovação corporativa**: conecta um problema de
chão de fábrica a uma ideia, a ideia a uma decisão fundamentada, a decisão a um projeto
formal e o projeto a um resultado de negócio medido.

```text
DIRETRIZ → IDEIA → ANÁLISE → APROVAÇÃO → PROJETO → EXECUÇÃO → RESULTADO
```

O foco é **governança, rastreabilidade e resultado** — não o registro de ideias.

---

## Visão do produto

Programas de inovação corporativa costumam falhar em três pontos, e o Flow foi desenhado
em torno deles.

**1. A ideia nasce desalinhada.** A tela inicial do operador abre pela **estratégia
vigente**, não por um formulário vazio. Dizer o que a empresa está buscando antes de pedir
uma ideia é o que faz a ideia nascer no lugar certo. E o primeiro campo do formulário é
*o problema*, não o título.

**2. A priorização é opaca.** Toda ideia recebe um **FlowScore** de 0 a 100, calculado por
uma fórmula pública derivada de RICE, WSJF e weighted scoring, com as cinco dimensões e
seus pesos visíveis na tela. O cálculo é determinístico, vive no domínio e **não depende de
IA nenhuma**: com a rede fora, a fila continua ordenada do mesmo jeito.

**3. Ninguém sabe o que aconteceu.** Cada transição gera uma entrada de auditoria
append-only *e* um snapshot completo do estado — na **mesma transação** do agregado. A
auditoria diz o que mudou; o snapshot diz como o projeto era naquele instante.

Sobre isso há uma camada inteligente que **aconselha e nunca decide**: um copiloto de
avaliação para o gestor, um rascunho de projeto que só existe depois que um humano confirma,
e insights executivos que citam a evidência de cada conclusão — ou dizem que não há
evidência suficiente.

### Perfis

| Perfil | O que faz |
|---|---|
| `Operator` | Propõe ideias alinhadas à estratégia, acompanha o retorno e ganha pontos |
| `Manager` | Avalia com critério visível, aprova, converte em projeto e conduz a execução |
| `Leadership` | Define a estratégia e lê o retorno do programa no painel executivo |

---

## Arquitetura

**Clean Architecture** em um **monólito modular**. As dependências apontam para dentro.

```text
        Mobile (Expo / React Native)
                   │  REST /api/v1
                   ▼
        ┌──────────────────────┐
        │      Flow.API        │  controllers, auth, rate limit, ProblemDetails
        ├──────────────────────┤
        │  Flow.Application    │  comandos, consultas, validação, contratos
        ├──────────────────────┤
        │    Flow.Domain       │  entidades, invariantes, máquinas de estado, FlowScore
        └──────────────────────┘
                   ▲
        ┌──────────────────────┐
        │  Flow.Infrastructure │  MongoDB · Identity · Gemini · OneSignal · OTel
        └──────────────────────┘
```

`Flow.Domain` não conhece MongoDB, HTTP nem SDK algum. `Flow.Application` fala com o mundo
por interfaces que ela mesma declara — inclusive `IUnitOfWork`, que expõe transação sem
vazar um único tipo do driver.

| Camada | Projeto | Responsabilidade |
|---|---|---|
| Domain | `src/Flow.Domain` | Entidades, enums, máquinas de estado, FlowScore |
| Application | `src/Flow.Application` | Comandos, consultas, handlers, validadores, DTOs |
| Infrastructure | `src/Flow.Infrastructure` | MongoDB, Identity, JWT, Gemini, OneSignal, telemetria |
| API | `src/Flow.API` | Controllers, middleware, Swagger, rate limiting |

Módulos funcionais: Auth · Guidelines · Ideas · Projects · Tracking · Results · Dashboard ·
Assistant · Notifications · Gamification.

Detalhes em [`docs/sprint-2/architecture.md`](docs/sprint-2/architecture.md).

---

## Stack

### Backend

| Item | Versão | Papel |
|---|---|---|
| .NET / C# | 8.0 | Runtime |
| ASP.NET Core | 8.0 | HTTP, autenticação, rate limiting nativo |
| MongoDB.Driver | 3.11.1 | **Driver oficial. Sem provider EF sobre Mongo** |
| MongoDB | 8.0 | Persistência, em replica set |
| ASP.NET Core Identity | 8.0 | Hash de senha e papéis, com stores próprios sobre Mongo |
| MediatR | 12.4.1 | Comandos e consultas |
| FluentValidation | 11.11.0 | Validação por pipeline behavior |
| Google.GenAI | 1.21.0 | Gemini com structured output |
| Serilog.AspNetCore | 10.0.0 | Log estruturado |
| OpenTelemetry | 1.18.0 | Traces e métricas |
| Swashbuckle | 6.9.0 | OpenAPI |

### Mobile

| Item | Versão |
|---|---|
| Expo SDK | 54 |
| React Native | 0.81.5 |
| React | 19.1.0 |
| TypeScript | 5.9 |
| TanStack Query | 5 |
| Zustand | 5 |
| victory-native + Skia | 41.19.3 + 2.2.12 |
| expo-secure-store | 15 |

### Testes

xUnit · FluentAssertions · Testcontainers.MongoDb 4.15.0 · `WebApplicationFactory`

---

## Requisitos

| Item | Versão | Obrigatório |
|---|---|---|
| .NET SDK | 8.0 | Sim |
| MongoDB | 8.0 em **replica set** | Sim |
| Node.js | 20+ | Só para o mobile |
| Docker | 24+ | Só para o caminho containerizado |

---

## MongoDB e o replica set

> **O replica set não é opcional.**

O Flow grava o agregado, a entrada de auditoria, o snapshot, o ledger de pontos e a
mensagem de outbox dentro de **uma transação multi-documento**. Transações não existem em
um `mongod` standalone.

Um Mongo avulso sobe, aceita leitura e escrita e **parece** funcionar — até a primeira
transição de projeto falhar. Em desenvolvimento e em teste, um replica set de **nó único**
é suficiente.

```bash
mongod --replSet rs0 --dbpath ./data --port 27017 --bind_ip 127.0.0.1
```

```bash
mongosh --eval 'rs.initiate({_id:"rs0", members:[{_id:0, host:"127.0.0.1:27017"}]})'
```

```bash
mongosh --eval 'rs.status().myState'
```

`myState` igual a `1` significa PRIMARY.

No startup a aplicação cria **41 índices** de forma idempotente e garante os três papéis do
Identity. O modelo documental completo, com os padrões de acesso que o justificam, está em
[`docs/sprint-2/data-model.md`](docs/sprint-2/data-model.md).

---

## Configuração

Copie `.env.example` para `.env` e preencha. **`.env` é gitignored e nenhum segredo real
existe no repositório.**

```bash
cp .env.example .env
```

### Variáveis

Existem duas grafias para a mesma configuração, e vale saber qual usar em cada caso.

**Com Docker Compose:** use os nomes planos da coluna à esquerda, no `.env`. Um arquivo
`.env` é um formato plano, e é o `docker-compose.yml` que traduz cada nome para o
hierárquico que a aplicação realmente vincula — `JWT_SECRET_KEY` vira
`JwtSettings__SecretKey`, e assim por diante.

**Sem Compose** — `dotnet run`, um PaaS que injeta variáveis direto no container, um
orquestrador — essa tradução não acontece. Ali valem `appsettings.json`,
`dotnet user-secrets` para desenvolvimento local, ou as variáveis hierárquicas com `__`
da coluna à direita. Segredo não entra em `appsettings.json` em nenhuma hipótese.

Três nomes são planos nos dois mundos, porque a aplicação os lê planos:
`CORS_ALLOWED_ORIGINS`, `SEED_DEMO_DATA` e `SEED_DEMO_PASSWORD`.

| `.env` (compose) | Variável da aplicação | Obrigatória |
|---|---|---|
| — | `Mongo__ConnectionString` | Sim |
| `MONGO_DATABASE` | `Mongo__Database` | Não (`flow`) |
| `JWT_SECRET_KEY` | `JwtSettings__SecretKey` | **Sim** |
| `JWT_ISSUER` | `JwtSettings__Issuer` | Não |
| `JWT_AUDIENCE` | `JwtSettings__Audience` | Não |
| `JWT_EXPIRY_MINUTES` | `JwtSettings__ExpiryMinutes` | Não (15) |
| `REFRESH_TOKEN_DAYS` | `Auth__RefreshTokenDays` | Não (7) |
| `GEMINI_API_KEY` | `Gemini__ApiKey` | Não |
| `GEMINI_MODEL` | `Gemini__Model` | Não |
| `ONESIGNAL_APP_ID` | `OneSignal__AppId` | Não |
| `ONESIGNAL_API_KEY` | `OneSignal__ApiKey` | Não |
| `FORWARDED_HEADERS_ENABLED` | `ForwardedHeaders__Enabled` | Atrás de proxy |
| `FORWARDED_TRUSTED_NETWORKS` | `ForwardedHeaders__TrustedNetworks__0` | Atrás de proxy |
| `FORWARDED_TRUSTED_PROXIES` | `ForwardedHeaders__TrustedProxies__0` | Alternativa à rede |
| `FORWARDED_LIMIT` | `ForwardedHeaders__ForwardLimit` | Não (1) |
| `OTLP_ENDPOINT` | `OpenTelemetry__OtlpEndpoint` | Não |
| `SWAGGER_ENABLED` | `Swagger__Enabled` | Não |
| `CORS_ALLOWED_ORIGINS` | `CORS_ALLOWED_ORIGINS` | Não |
| `SEED_DEMO_DATA` | `SEED_DEMO_DATA` | Não |
| `SEED_DEMO_PASSWORD` | `SEED_DEMO_PASSWORD` | Só com o seed ligado |

Para gerar um segredo JWT:

```bash
openssl rand -base64 48
```

> A API **recusa iniciar** fora de Development em duas situações, pelo mesmo motivo: uma
> configuração de segurança que falha em silêncio é pior do que uma que falha alto. São
> elas o segredo JWT ainda no placeholder ou com menos de 32 bytes, e
> `ForwardedHeaders__Enabled` ligado sem nenhum proxy ou rede confiável declarados — aí os
> cabeçalhos seriam ignorados e todos os clientes dividiriam um balde de rate limit,
> parecendo configurado.

**Atrás de proxy reverso** (Traefik no Dokploy), ligue `ForwardedHeaders__Enabled` e declare
a rede do proxy. Sem isso a API vê o endereço do Traefik para todo mundo e o esquema como
`http`. Detalhes e o porquê de ser uma permissão explícita em
[`deployment.md`](docs/sprint-2/deployment.md#45-forwarded-headers--obrigatório-atrás-do-proxy).

**Chaves opcionais são opcionais de verdade.** Sem `Gemini__ApiKey` os endpoints
inteligentes respondem `503` e o resto do produto funciona igual. Sem credencial OneSignal
a central de avisos dentro do app continua funcionando e as mensagens de outbox ficam
**pendentes** — nada é marcado como entregue.

### Onde os segredos nunca podem estar

| Segredo | Onde vive | Onde **nunca** vive |
|---|---|---|
| Segredo JWT | Variável de ambiente | Repositório, imagem, log |
| Chave do Gemini | Variável de ambiente, **só no servidor** | App mobile, log, `assistant_runs` |
| REST API key do OneSignal | Variável de ambiente, **só no servidor** | App mobile |
| App ID do OneSignal | Perfil EAS | — é público por natureza |
| Senha do MongoDB | Connection string por variável | Repositório |

---

## Execução local

### Backend

```bash
dotnet restore
```

```bash
export Mongo__ConnectionString="mongodb://127.0.0.1:27017/?replicaSet=rs0"
```

```bash
export JwtSettings__SecretKey="$(openssl rand -base64 48)"
```

```bash
dotnet run --project src/Flow.API
```

A API sobe em `http://localhost:5153` (e `https://localhost:7296`). O Swagger fica em
`/swagger`, com os **54 endpoints** documentados.

### Mobile

```bash
cd mobile && npm ci
```

```bash
npx expo start
```

A URL da API se resolve sozinha: em desenvolvimento o app usa o host que serve o bundle,
então um aparelho na mesma rede encontra a máquina sem editar nada. Para apontar para outro
ambiente, defina `EXPO_PUBLIC_API_URL`.

---

## Docker

```bash
cp .env.example .env
```

```bash
docker compose up -d
```

O compose sobe dois serviços:

- **mongo** — `mongo:8.0` com `--replSet rs0`. O healthcheck executa o `rs.initiate` na
  primeira subida e só reporta saudável quando o nó é PRIMARY;
- **api** — imagem multi-stage com `depends_on: service_healthy`, de modo que a API só
  inicia quando o replica set está pronto.

A imagem compila em `sdk:8.0-alpine`, roda em `aspnet:8.0-alpine` como usuário **não
privilegiado**, faz o `restore` em camada separada, aponta o `HEALTHCHECK` para
`/health/ready` — não `/health/live`, porque o orquestrador só deve encaminhar tráfego
quando o MongoDB estiver alcançável — e **não carrega segredo nenhum**.

> O `Dockerfile` e o `docker-compose.yml` estão escritos e revisados, mas **não foram
> construídos nem executados** na máquina de desenvolvimento — o Docker Desktop local não
> inicia. Ver [`docs/sprint-2/deployment.md`](docs/sprint-2/deployment.md#7-estado-de-verificação).

---

## Testes

```bash
export FLOW_TEST_MONGO_URI="mongodb://127.0.0.1:27017/?replicaSet=rs0"
```

```bash
dotnet test
```

```text
Flow.Domain.Tests          124   invariantes, máquinas de estado, FlowScore
Flow.Application.Tests      10   aritmética do dashboard nos casos de borda
Flow.Architecture.Tests      9   fronteiras entre as camadas
Flow.Integration.Tests     170   MongoDB real, transações reais, API ponta a ponta
─────────────────────────────
Total                      313
```

Os testes de integração usam um **MongoDB real e descartável**. Se `FLOW_TEST_MONGO_URI`
estiver definido, é ele; caso contrário o fixture sobe um container por Testcontainers.
**Nenhum fake substitui o banco nos fluxos críticos.**

O teste que mais importa é o de integridade transacional: ele injeta uma falha na escrita
de auditoria no meio da aprovação de uma ideia e verifica que o status não mudou, os pontos
não foram creditados e a notificação não foi criada.

```bash
dotnet test --filter "FullyQualifiedName~TransactionalIntegrity"
```

---

## Demonstração

```bash
export SEED_DEMO_DATA=true
```

```bash
export SEED_DEMO_PASSWORD='FlowDemo!2026'
```

```bash
dotnet run --project src/Flow.API
```

O seed é **idempotente** e cria um conjunto narrativo: 5 diretrizes em 3 campanhas (uma já
encerrada), 10 ideias em todos os estados, 6 projetos — entregues, em execução, bloqueado,
planejado e cancelado —, resultados com ROI e ganhos não financeiros, pontos, comentários,
notificações e trilha de auditoria distribuída ao longo de 6 meses.

| Papel | E-mail |
|---|---|
| Operador | `operator@flow.demo` |
| Operador | `bruno@flow.demo` |
| Gestora | `manager@flow.demo` |
| Liderança | `leadership@flow.demo` |

A senha é a de `SEED_DEMO_PASSWORD`. Ela existe só para o ambiente de demonstração, vem por
variável de ambiente e nunca é commitada. **Nunca ligue o seed em produção.**

O roteiro completo, de 12 a 15 minutos, está em
[`docs/sprint-2/demo-script.md`](docs/sprint-2/demo-script.md).

---

## APK Android

```bash
cd mobile
```

Para conferir que o projeto empacota, sem credencial nenhuma:

```bash
npx expo export --platform android
```

Para gerar o APK instalável (**exige credencial EAS**):

```bash
eas build --platform android --profile preview
```

| Perfil | Saída | Uso |
|---|---|---|
| `development` | APK com dev client | Depuração contra API local |
| `preview` | **APK** | Distribuição interna e instalação direta |
| `production` | **APK** | Entrega |
| `production-store` | AAB | Google Play |

Ajuste `EXPO_PUBLIC_API_URL` no perfil antes de gerar o build.

> O bundle Android foi gerado e verificado (5,59 MB, Hermes) e o `expo-doctor` passa em
> 18/18. O **APK em si não foi gerado**: falta credencial EAS com assinatura Android.

---

## Deploy

Produção com Docker atrás de Traefik — o `docker-compose.yml` e o `Dockerfile` servem tanto
para desenvolvimento quanto para o PaaS.

1. Provisionar um MongoDB **em replica set** (uma instância avulsa não serve);
2. Criar a aplicação apontando para o repositório, Dockerfile na raiz, porta interna
   **8080**, health check em `/health/ready`;
3. Definir as variáveis hierárquicas da tabela acima, com `Swagger__Enabled=false` e
   `SEED_DEMO_DATA=false`;
4. Associar o domínio e habilitar o certificado; o TLS termina no proxy.

```bash
curl -fsS https://flow-api.example.com/health/ready
```

O passo a passo, incluindo as labels de Traefik e o estado real de verificação de cada
etapa, está em [`docs/sprint-2/deployment.md`](docs/sprint-2/deployment.md).

---

## Observabilidade

| Sinal | Onde |
|---|---|
| Logs estruturados | Serilog, console, com contexto de requisição |
| Traces | ASP.NET Core, HttpClient e MongoDB |
| Métricas | 11 de negócio + as de runtime, via OpenTelemetry |
| Liveness | `GET /health/live` |
| Readiness | `GET /health/ready` |

Todo erro devolve **RFC 7807 ProblemDetails** com um `traceId` — e é o mesmo identificador
que aparece no log, no trace e em `audit_logs.correlationId`. Sem coletor OTLP configurado
a instrumentação segue ativa em processo e a API sobe normalmente.

Detalhes, incluindo o que **nunca** é registrado, em
[`docs/sprint-2/observability.md`](docs/sprint-2/observability.md).

---

## API

Todas as rotas são versionadas sob `/api/v1`. São **54 endpoints**.

| Área | Rotas | Resumo |
|---|---|---|
| Auth | 4 | Registro, login, refresh com rotação, logout |
| Guidelines | 8 | CRUD de diretrizes, vigente, encerramento, histórico |
| Ideas | 14 | Ciclo completo, comentários, prioridade, nota, FlowScore, comparação |
| Projects | 14 | Criação, conversão, progresso, etapa, bloqueio, timeline, snapshots |
| Results | 2 | Estimado e realizado, ROI, ganhos não financeiros |
| Dashboard | 4 | Painel executivo, insights, detalhe por projeto e por diretriz |
| Assistant | 2 | Copiloto de avaliação e rascunho de projeto |
| Notifications | 3 | Central de avisos e marcação de leitura |
| Users | 3 | Pontos e ledger, próprio e de terceiros |

A referência completa, com método, rota, papel exigido e respostas, está em
[`docs/sprint-2/endpoints.md`](docs/sprint-2/endpoints.md). O `openapi.json` pode ser
exportado da própria aplicação:

```bash
./scripts/build-artifacts.sh
```

---

## Governança e rastreabilidade

A trilha de auditoria faz parte do modelo de negócio, não é um detalhe de infraestrutura.

- mudanças de estado passam pela lógica de domínio;
- entidades auditadas não são alteradas direto pelo controller;
- toda transição relevante gera `AuditLog`;
- toda transição de projeto gera `ProjectSnapshot`;
- **a transição, o log, o snapshot, o ledger de pontos e o outbox commitam juntos**;
- rejeições, cancelamentos e bloqueios exigem contexto no histórico.

### Estados

Ideias:

```text
Draft → UnderReview → Approved
                  └→ Rejected
```

Projetos — situação de governança:

```text
Planned → InProgress → Completed
   │          ├──────→ Cancelled
   │          └──────→ Blocked
   └────────────────→ Blocked

Blocked → InProgress
Blocked → Cancelled
```

Etapa de execução (`Discovery → Design → Build → Validation → Rollout`) é **ortogonal** à
situação: um projeto bloqueado permanece na etapa que alcançou.

`Blocked` é um estado de primeira classe e alimenta o índice de gargalo do dashboard.

---

## Documentação

### Sprint 2

| Documento | Conteúdo |
|---|---|
| [`compliance-matrix.md`](docs/sprint-2/compliance-matrix.md) | Requisito a requisito, com evidência e estado |
| [`architecture.md`](docs/sprint-2/architecture.md) | Decisões da migração e desenho das camadas |
| [`data-model.md`](docs/sprint-2/data-model.md) | Coleções, índices, padrões de acesso |
| [`flowscore.md`](docs/sprint-2/flowscore.md) | Fórmula, exemplos e propriedades garantidas |
| [`ai-integration.md`](docs/sprint-2/ai-integration.md) | Capacidades, resiliência e governança do assistente |
| [`endpoints.md`](docs/sprint-2/endpoints.md) | Referência dos 54 endpoints |
| [`observability.md`](docs/sprint-2/observability.md) | Logs, traces, métricas e health checks |
| [`demo-script.md`](docs/sprint-2/demo-script.md) | Roteiro de demonstração |
| [`deployment.md`](docs/sprint-2/deployment.md) | Local, Docker e produção |
| [`delivery-checklist.md`](docs/sprint-2/delivery-checklist.md) | Estado real de cada entregável |

### Geral

- [`ENGINEERING.md`](ENGINEERING.md) — regras de engenharia e definição de pronto
- [`PROJECT_DECISIONS.md`](PROJECT_DECISIONS.md) — decisões de produto e arquitetura
- [`docs/specs/`](docs/specs/) — especificações do MVP e do visual do mobile
- [`docs/design-system.md`](docs/design-system.md) — design system

---

## Estrutura

```text
Flow.sln
Dockerfile
docker-compose.yml
.env.example

src/
  Flow.Domain/          entidades, invariantes, FlowScore
  Flow.Application/     comandos, consultas, contratos
  Flow.Infrastructure/  MongoDB, Identity, Gemini, OneSignal, telemetria
  Flow.API/             controllers, middleware, configuração

tests/
  Flow.Domain.Tests/
  Flow.Application.Tests/
  Flow.Integration.Tests/

mobile/
  src/                  api, components, navigation, screens, store, notifications

scripts/
  build-artifacts.sh

docs/
  sprint-2/             documentação desta sprint
  api/ architecture/ mobile/ product/ specs/
```
