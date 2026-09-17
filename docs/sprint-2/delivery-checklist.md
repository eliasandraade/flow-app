# Checklist de entrega — Sprint 2

Estado real de cada item, verificado contra o código e a execução, não contra a intenção.

### Origem da Sprint 2

A Sprint 2 foi desenvolvida no repositório de desenvolvimento
[`eliasandraade/flow`](https://github.com/eliasandraade/flow), na branch `sprint-2`, a partir
da base `ff00816` (`master`). O último commit dessa branch é `0925d04`, e o registro de como
se chegou até ele está em [`compliance-matrix.md`](compliance-matrix.md).

### Snapshot público

Este repositório, [`eliasandraade/flow-app`](https://github.com/eliasandraade/flow-app), é a
entrega pública. O commit que publicou o conteúdo, `410ddf8`, tem **exatamente a mesma árvore**
(`0a9fea0`) que o `0925d04` da origem: o código publicado é, byte a byte, o que foi validado
lá. O que mudou depois foi só o alinhamento ao repositório público: os gatilhos da CI, o
teste que guarda esses gatilhos e esta documentação. Nenhum código de aplicação.

### Branch pública

`main` é a única branch. A CI ([`ci.yml`](../../.github/workflows/ci.yml)) roda em todo push e
em todo pull request para ela; o estado atual aparece no badge do README.

### Validação atual

Executada neste repositório, não herdada da origem:

- **Build:** `dotnet build Flow.sln -c Release` — 0 erros, 0 avisos
- **Testes .NET:** 332, todos verdes, nenhum ignorado
- **Contrato do OpenAPI:** 10 de 10 (`node --test scripts/*.test.mjs`)
- **Contrato de empacotamento:** 15 de 15 verificações (`scripts/build-artifacts.contract.test.sh`)
- **Mobile:** `tsc --noEmit` limpo, `expo-doctor` 18/18, bundle Android gerado

Legenda: ✅ verificado em execução · ⏳ pendente de credencial ou ambiente

---

## 1. Requisitos do Challenge

| Requisito | Estado | Evidência |
|---|---|---|
| Backend em C#/.NET 8 | ✅ | `net8.0` nos quatro projetos |
| Autenticação | ✅ | `AuthTests`, 12 cenários |
| Três perfis | ✅ | `Operator`, `Manager`, `Leadership` |
| JWT | ✅ | Emissão, validação e claims de papel |
| Autorização por nível | ✅ | Matriz por papel + autorização por recurso |
| Orientações estratégicas | ✅ | CRUD, vigência, campanha, categoria, histórico |
| Gerenciamento de ideias | ✅ | Ciclo completo, 14 endpoints |
| Priorização e aprovação | ✅ | Prioridade, nota, FlowScore, aprovar, rejeitar |
| Gerenciamento de projetos | ✅ | Máquina de estados, etapa, progresso |
| Progresso e resultados | ✅ | Operação dedicada + estimado/realizado |
| Dashboard | ✅ | 1 endpoint, ~40 métricas, datasets prontos |
| Integração real com o mobile | ✅ | REST, sem mocks |
| APIs funcionais sem mock | ✅ | 182 testes de integração contra Mongo real |
| MongoDB ou outro NoSQL | ✅ | MongoDB 8.0, driver oficial 3.11.1 |
| Serviços externos | ✅ | Gemini e OneSignal implementados |
| Auditoria | ✅ | Append-only, transacional |
| Logs | ✅ | Serilog estruturado |
| Métricas | ✅ | OpenTelemetry, 11 métricas de negócio |
| APK Android | ⏳ | Configuração pronta; falta credencial EAS |

---

## 2. Banco de dados

| Item | Estado |
|---|---|
| EF Core e SQL Server removidos de `src/` | ✅ |
| `MongoDB.Driver` 3.11.1, sem provider EF | ✅ |
| Modelo documental derivado dos access patterns | ✅ [`data-model.md`](data-model.md) |
| 41 índices criados de forma idempotente | ✅ |
| Replica set com transações reais | ✅ verificado com commit e abort |
| `IUnitOfWork` sem vazar `IClientSessionHandle` | ✅ |
| Auditoria e snapshot append-only por construção | ✅ |
| `Decimal128` para valor financeiro | ✅ verificado por teste de precisão |
| Rollback do agregado quando a auditoria falha | ✅ `TransactionalIntegrityTests` |

---

## 3. Backend

| Item | Estado |
|---|---|
| Clean Architecture preservada | ✅ EF fora da Application |
| Identity sobre Mongo, sem pacote comunitário | ✅ 6 contratos |
| Rotação e hash de refresh token | ✅ |
| Detecção de replay revogando a cadeia | ✅ |
| FluentValidation ativo via behavior | ✅ 22 validadores |
| ProblemDetails RFC7807 com `traceId` | ✅ |
| CORS explícito | ✅ |
| Rate limiting | ✅ verificado por teste |
| Segredos por variável de ambiente | ✅ |
| Validação do segredo JWT no startup | ✅ |

---

## 4. FlowScore

| Item | Estado |
|---|---|
| Fórmula documentada antes da implementação | ✅ [`flowscore.md`](flowscore.md) |
| Fundamentada em RICE, WSJF e weighted scoring | ✅ |
| Score 0..100 com componentes visíveis | ✅ |
| Independente do Gemini | ✅ verificado com provedor fora |
| Entrada manual soberana | ✅ nota e FlowScore coexistem |
| Propriedades garantidas por teste | ✅ 12 testes, P1 a P7 |

---

## 5. Funcionalidades inteligentes

| Item | Estado |
|---|---|
| Contratos neutros na Application | ✅ |
| Structured output com schema explícito | ✅ |
| Copiloto contextual, não caixa de texto | ✅ 3 operações tipadas |
| Rascunho de projeto com confirmação humana | ✅ verificado: nada é criado |
| Insights executivos com evidência | ✅ |
| Recusa de análise sem evidência | ✅ `evidenceWasSufficient` |
| Timeout, circuit breaker, sem retry cego | ✅ 5 testes de breaker |
| Governança em `assistant_runs` | ✅ inclusive falhas |
| Chave apenas server-side | ✅ |
| **Chamada real ao `gemini-3.8-flash`** | ✅ validada contra o serviço real — ver [§13](#13-validação-real-do-gemini) |

---

## 6. Notificações

| Item | Estado |
|---|---|
| Central persistida no Flow | ✅ |
| Outbox na mesma transação do domínio | ✅ |
| `HostedService` de despacho | ✅ |
| Retry com backoff e jitter | ✅ |
| Dead-letter | ✅ |
| Idempotência por índice único | ✅ verificado por teste |
| Sem credencial, nada é marcado como enviado | ✅ verificado por teste |
| **Push real entregue** | ⏳ falta credencial OneSignal/FCM |

---

## 7. Observabilidade

| Item | Estado |
|---|---|
| Serilog estruturado | ✅ |
| Traces ASP.NET Core, HttpClient e MongoDB | ✅ |
| Métricas técnicas e de negócio | ✅ 11 métricas |
| `/health/live` e `/health/ready` | ✅ |
| `correlationId` ligando erro, log, trace e auditoria | ✅ |
| Sem label de alta cardinalidade | ✅ |
| API sobe sem coletor OTLP | ✅ |
| **Visualização em um coletor** | ⏳ sem coletor neste ambiente |

---

## 8. Mobile

| Item | Estado |
|---|---|
| URL por ambiente | ✅ |
| Refresh transparente com single-flight | ✅ |
| Jornada do Operador | ✅ |
| Jornada do Gestor | ✅ |
| Jornada da Liderança | ✅ |
| Compartilhado: estratégia, avisos, perfil | ✅ |
| Charts | ✅ spike documentado, 18/18 |
| Loading, empty e error com retry | ✅ |
| Interface em pt-BR | ✅ inclusive as mensagens de erro do servidor |
| Cliente tipado com ProblemDetails | ✅ |
| OneSignal por external id | ✅ implementado |
| `tsc --noEmit` limpo | ✅ |
| `expo-doctor` | ✅ 18/18 |
| Bundle Android | ✅ 5,59 MB Hermes |
| **APK instalado em aparelho** | ⏳ falta credencial EAS |

---

## 9. Testes

```text
Flow.Domain.Tests          124   invariantes, máquinas de estado, FlowScore
Flow.Application.Tests      10   aritmética do dashboard nos casos de borda
Flow.Architecture.Tests     16   fronteiras entre as camadas e contrato da CI
Flow.Integration.Tests     182   MongoDB real, transações reais, API ponta a ponta
─────────────────────────────
Total                      332   0 falhas, 0 ignorados

Contratos dos scripts de entrega
check-openapi.test.mjs              10   origem e identidade do openapi.json
build-artifacts.contract.test.sh    15   nenhuma entrega com suíte vermelha
```

| Área exigida | Cobertura |
|---|---|
| AUTH | login, papéis, refresh, rotação, replay, logout, 401, 403 |
| STRATEGY | CRUD, leitura por outros, vigência, histórico |
| IDEA | criar, editar, excluir, submeter, comentar, pontuar, aprovar, rejeitar, autorização |
| PROJECT | criar, converter, progresso, etapa, bloquear, desbloquear, concluir, cancelar, snapshots, auditoria |
| RESULT | estimado, realizado, ROI, produtividade, precisão decimal |
| DASHBOARD | agregações, base vazia, base realista |
| TRANSACTION | rollback do domínio quando a auditoria falha |
| AI | contrato, parsing, resposta malformada, timeout, indisponível, autorização, sem mutação |
| NOTIFICATION | outbox, retry, dead-letter, idempotência |

---

## 10. Entregáveis

| Item | Estado |
|---|---|
| `Dockerfile` multi-stage, usuário não-root | ✅ escrito |
| `docker-compose.yml` com replica set | ✅ escrito |
| `.env.example` sem segredo real | ✅ |
| `scripts/build-artifacts.sh` | ✅ fail-closed: não empacota com suíte vermelha nem publica spec de outra origem |
| Export de `openapi.json` | ✅ 54 endpoints, 73 schemas |
| `eas.json` com profile APK | ✅ |
| 10 documentos em `docs/sprint-2/` | ✅ os 9 exigidos + `flowscore.md` |
| README atualizado | ✅ |
| Build da imagem Docker | ✅ construída e executada na CI |
| **Deploy HTTPS no Dokploy** | ⏳ falta credencial |

---

## 11. Pendências reais

Dois itens, **ambos** dependendo de credencial externa. Nenhum depende de código que falte
escrever, e nenhum pode ser resolvido por engenharia — só por acesso.

| # | Pendência | Bloqueio | O que falta |
|---|---|---|---|
| 1 | Push real via OneSignal | Sem credencial OneSignal/FCM | Definir App ID e REST API key |
| 2 | APK assinado e deploy HTTPS | Sem credencial EAS e Dokploy | `eas build -p android --profile preview`; publicar no Dokploy |

A chamada real ao Gemini **deixou de ser pendência**: foi executada contra o serviço real em
17/09/2026 e está registrada na [§13](#13-validação-real-do-gemini).

O build da imagem e o `docker compose up` **deixaram de ser pendência**: o job de Docker da
CI faz os dois a cada push, em runner limpo.

### Sobre o Docker nesta máquina

O Docker Desktop está instalado, os processos iniciam, mas a distro WSL `docker-desktop`
fica em `Stopped` e `docker desktop status` trava. Foram tentadas inicialização direta,
`docker desktop start` e restart completo com `wsl --shutdown`. Isso continua assim.

Não bloqueou o trabalho em nenhum momento. Localmente, os 182 testes de integração rodam
contra um **MongoDB 8.0.30 real em replica set de nó único** — com transações reais, commit
e abort verificados — e o fixture aceita `FLOW_TEST_MONGO_URI` exatamente para isso, caindo
em Testcontainers onde houver daemon. Na CI, onde há daemon, é Testcontainers que sobe o
banco, e a imagem Docker é construída e executada de verdade.

---

## 12. Como verificar tudo

```bash
# backend
dotnet build Flow.sln -c Release      # 0 erros, 0 avisos
export FLOW_TEST_MONGO_URI="mongodb://127.0.0.1:27017/?replicaSet=rs0"
dotnet test Flow.sln                  # 332 testes

# contratos dos scripts de entrega
node --test scripts/*.test.mjs                  # 10 testes
bash scripts/build-artifacts.contract.test.sh   # 15 verificações, precisa de MongoDB

# mobile
cd mobile
npx tsc --noEmit                      # sem saída
npx expo-doctor                       # 18/18
npx expo export --platform android    # bundle gerado

# artefatos
./scripts/build-artifacts.sh          # dist/
```

---

## 13. Validação real do Gemini

Executada em 17/09/2026 contra o serviço real, pelo caminho completo da aplicação:
HTTP → `DashboardController` → `GenerateExecutiveInsightsCommand` →
`GeminiExecutiveInsightService` → `GeminiStructuredClient` → Gemini → parsing → HTTP.
Nenhuma linha de código ou de configuração foi alterada para isso.

| Item | Resultado |
|---|---|
| Endpoint | `POST /api/v1/dashboard/insights`, perfil Leadership |
| Modelo | `gemini-3.8-flash`, o mesmo já configurado |
| HTTP | **200** |
| Latência | 18,0 s (uma execução anterior levou 11,1 s) |
| Structured output | aceito: `responseMimeType: application/json` com `responseJsonSchema` |
| Parsing | bem-sucedido, sem fallback para texto livre |
| Tokens | 2.365 de prompt, 2.145 de resposta |
| Conteúdo | resumo executivo, 3 destaques, 3 riscos, 2 oportunidades, 2 recomendações |
| Evidências | 31 citações no formato `campo = valor`, e `evidenceWasSufficient: true` |
| `assistant_runs` | registro com `outcome = Success`, modelo, latência, tokens e `correlationId` |

O banco foi um MongoDB 8.0 real em replica set, com o seed de demonstração — os números
citados pelo modelo são os do painel daquele conjunto de dados.

### Disponibilidade observada do provedor

De 14 execuções registradas em `assistant_runs` durante a validação, 2 terminaram em
`Success` e as demais em `Unavailable` ou `Timeout`. A causa está do lado do provedor e vem
identificada na própria resposta:

```text
503 UNAVAILABLE: This model is currently experiencing high demand.
Spikes in demand are usually temporary. Please try again later.
```

O Flow trata cada uma dessas exatamente como o contrato prevê: `503` com `ProblemDetails`,
`traceId`, o restante do produto funcionando e o `assistant_run` registrando a falha com o
tipo correto. O cliente não faz retry por decisão de projeto, já registrada — geração de
conteúdo é um POST medido e não idempotente. Com a taxa de indisponibilidade observada
acima, um retry com backoff seria a evolução natural, e fica anotado como decisão em
aberto, não como defeito.

### Caminhos de falha, verificados na mesma rodada

| Cenário | Resultado |
|---|---|
| Sem chave | `503`, `errorKind = NotConfigured`, latência 0, aplicação de pé |
| Chave inválida | `503`, `errorKind = Unavailable`, chamada externa real de 654 ms, nada sensível no log |
| Timeout e circuit breaker | cobertos pela suíte automatizada, sem provocar o serviço real |
