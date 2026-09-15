# Observabilidade

O que o Flow emite, por que emite, e o que deliberadamente **não** emite.

- **Logs:** Serilog estruturado
- **Traces e métricas:** OpenTelemetry 1.18.0
- **Health:** `/health/live` e `/health/ready`
- **Exportação:** OTLP, opcional

---

## 1. Princípio

Uma instrumentação que derruba o serviço quando o coletor cai é pior que nenhuma. Tudo
aqui é projetado para degradar em silêncio: sem endpoint OTLP configurado, a
instrumentação continua ativa em processo e a API sobe normalmente.

---

## 2. Logs

Serilog é configurado antes de qualquer outra coisa, para que uma falha de startup também
saia estruturada.

Cada evento carrega:

| Campo | Origem |
|---|---|
| `service` | `flow-api` |
| `MachineName` | host |
| `RequestId`, `RequestPath` | ASP.NET Core |
| `SourceContext` | tipo que emitiu |
| `TraceId`, `SpanId` | Activity corrente |

Requisições passam por `UseSerilogRequestLogging`, com o nível derivado do resultado:
`Error` para 5xx e exceções, `Verbose` para health check — que de outro modo dominaria o
log — e `Information` para o restante.

### O que nunca é logado

| Nunca | Por quê |
|---|---|
| `GEMINI_API_KEY`, `ONESIGNAL_API_KEY` | Exceções de provedor podem ecoar o request, e o request carrega o header de autenticação. Por isso a exceção do Gemini é logada **por tipo**, não por completo. |
| Access token e refresh token | Um log com token é uma sessão sequestrável. |
| Senha e hash de senha | — |
| Corpo de resposta do assistente | Pode conter conteúdo de negócio; ele já vive em `assistant_runs`, com controle de acesso. |

---

## 3. Traces

`ActivitySource` **Flow**, instrumentando:

| Fonte | O que aparece |
|---|---|
| ASP.NET Core | Requisição, rota, status. Health checks filtrados. |
| `HttpClient` | Chamadas ao OneSignal |
| MongoDB | Cada comando, via `MongoDB.Driver.Core.Extensions.DiagnosticSources` |

A instrumentação do Mongo importa mais do que parece: sem ela, o tempo gasto em agregação
aparece como latência inexplicada dentro do handler. Com ela, um dashboard lento mostra
qual `$facet` custou.

`CaptureCommandText` fica **desligado**: o texto do comando carrega valores de documento,
incluindo os campos de negócio.

### Correlação ponta a ponta

O mesmo identificador aparece em quatro lugares:

```text
Activity.TraceId
   ├── ProblemDetails.traceId      devolvido ao cliente no erro
   ├── audit_logs.correlationId    gravado com a transição
   ├── assistant_runs.correlationId
   └── log estruturado
```

Um usuário relata "deu erro, código 0af76519…" e isso basta para achar a requisição, o
trace, a decisão auditada e a linha de log.

---

## 4. Métricas

Meter **Flow**, além de ASP.NET Core, `HttpClient` e runtime.

### Métricas de negócio

| Métrica | Tipo | Labels |
|---|---|---|
| `flow_ideas_submitted` | contador | — |
| `flow_ideas_approved` | contador | — |
| `flow_ideas_rejected` | contador | — |
| `flow_projects_created` | contador | — |
| `flow_projects_blocked` | contador | — |
| `flow_projects_completed` | contador | — |
| `flow_ai_requests` | contador | `operation` |
| `flow_ai_failures` | contador | `operation`, `kind` |
| `flow_ai_latency_ms` | histograma | `operation` |
| `flow_notifications_sent` | contador | — |
| `flow_notifications_failed` | contador | `kind` |

### Por que não há `userId` como label

Cardinalidade. Uma métrica com `userId` cria uma série temporal por usuário, e o backend
de métricas degrada muito antes de responder algo útil. Atribuição por usuário já existe,
com melhor granularidade, em `audit_logs`, `assistant_runs` e nos traces.

Os labels que existem — `operation` e `kind` — são conjuntos fechados e pequenos.

---

## 5. Health checks

| Endpoint | Verifica | Uso |
|---|---|---|
| `/health/live` | O processo responde | Liveness probe |
| `/health/ready` | MongoDB alcançável | Readiness probe e healthcheck do container |

```json
{
  "status": "Healthy",
  "totalDurationMs": 40.68,
  "checks": [{ "name": "mongodb", "status": "Healthy", "durationMs": 28.62, "description": null }]
}
```

Separá-los é o que impede um orquestrador de matar um pod saudável durante uma oscilação
momentânea do banco: `live` continua verde, só `ready` fica vermelho e o tráfego é
desviado.

A mensagem de exceção é **omitida** do payload de propósito — ela pode conter connection
string e hostname interno, e o endpoint não é autenticado.

---

## 6. Exportação OTLP

```bash
OTLP_ENDPOINT=http://otel-collector:4317
```

Sem essa variável, nenhum exportador é registrado, a instrumentação segue coletando em
processo e a aplicação sobe normalmente. Com ela, traces e métricas vão para o coletor.

Qualquer backend compatível com OTLP serve: Jaeger, Tempo, Prometheus via collector,
Application Insights, Datadog.

---

## 7. Demonstração

```bash
# 1. health
curl -s localhost:5153/health/live  | jq
curl -s localhost:5153/health/ready | jq

# 2. um erro correlacionado — o traceId liga tudo
curl -s -X POST localhost:5153/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"nao@existe.com","password":"errada"}' | jq

# 3. o mesmo traceId aparece no log estruturado da API

# 4. a auditoria de uma transição carrega o correlationId da requisição que a causou
```

---

## 8. Estado de verificação

| Item | Estado |
|---|---|
| Serilog estruturado com enriquecimento | ✅ verificado em execução |
| Traces ASP.NET Core, HttpClient e MongoDB | ✅ registrado e ativo |
| Métricas técnicas e de negócio | ✅ registrado e ativo |
| `/health/live` e `/health/ready` | ✅ verificado em execução |
| `correlationId` na auditoria | ✅ verificado por teste de integração |
| API sobe sem coletor OTLP | ✅ é como a suíte inteira roda |
| Traces e métricas **visualizados em um coletor** | ⏳ não executado neste ambiente |

O último item é honesto: não há coletor OTLP neste ambiente de desenvolvimento. A
instrumentação está registrada e ativa, e a exportação é uma variável de ambiente — mas a
visualização em Jaeger ou Prometheus não foi executada aqui.
