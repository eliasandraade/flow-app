# Checklist de entrega — Sprint 2

Estado real de cada item, verificado contra o código e a execução, não contra a intenção.

- **Branch:** `sprint-2`
- **Base:** `ff00816` (`master`)
- **Build:** `dotnet build Flow.sln` — 0 erros, 0 avisos
- **Testes:** 313, todos verdes
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
| APIs funcionais sem mock | ✅ | 170 testes de integração contra Mongo real |
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
| **Chamada real ao `gemini-3.8-flash`** | ⏳ falta `GEMINI_API_KEY` |

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
Flow.Architecture.Tests      9   fronteiras entre as camadas
Flow.Integration.Tests     170   MongoDB real, transações reais, API ponta a ponta
─────────────────────────────
Total                      313   0 falhas
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
| `scripts/build-artifacts.sh` | ✅ |
| Export de `openapi.json` | ✅ 54 endpoints, 73 schemas |
| `eas.json` com profile APK | ✅ |
| 10 documentos em `docs/sprint-2/` | ✅ os 9 exigidos + `flowscore.md` |
| README atualizado | ✅ |
| Build da imagem Docker | ✅ construída e executada na CI |
| **Deploy HTTPS no Dokploy** | ⏳ falta credencial |

---

## 11. Pendências reais

Três itens, **todos** dependendo de credencial externa. Nenhum depende de código que falte
escrever, e nenhum pode ser resolvido por engenharia — só por acesso.

| # | Pendência | Bloqueio | O que falta |
|---|---|---|---|
| 1 | Chamada real ao Gemini | Sem `Gemini__ApiKey` | Definir a variável e chamar `POST /dashboard/insights` |
| 2 | Push real via OneSignal | Sem credencial OneSignal/FCM | Definir App ID e REST API key |
| 3 | APK assinado e deploy HTTPS | Sem credencial EAS e Dokploy | `eas build -p android --profile preview`; publicar no Dokploy |

O build da imagem e o `docker compose up` **deixaram de ser pendência**: o job de Docker da
CI faz os dois a cada push, em runner limpo.

### Sobre o Docker nesta máquina

O Docker Desktop está instalado, os processos iniciam, mas a distro WSL `docker-desktop`
fica em `Stopped` e `docker desktop status` trava. Foram tentadas inicialização direta,
`docker desktop start` e restart completo com `wsl --shutdown`. Isso continua assim.

Não bloqueou o trabalho em nenhum momento. Localmente, os 170 testes de integração rodam
contra um **MongoDB 8.0.30 real em replica set de nó único** — com transações reais, commit
e abort verificados — e o fixture aceita `FLOW_TEST_MONGO_URI` exatamente para isso, caindo
em Testcontainers onde houver daemon. Na CI, onde há daemon, é Testcontainers que sobe o
banco, e a imagem Docker é construída e executada de verdade.

---

## 12. Como verificar tudo

```bash
# backend
dotnet build Flow.sln                 # 0 erros, 0 avisos
export FLOW_TEST_MONGO_URI="mongodb://127.0.0.1:27017/?replicaSet=rs0"
dotnet test Flow.sln                  # 313 testes

# mobile
cd mobile
npx tsc --noEmit                      # sem saída
npx expo-doctor                       # 18/18
npx expo export --platform android    # bundle gerado

# artefatos
./scripts/build-artifacts.sh          # dist/
```
