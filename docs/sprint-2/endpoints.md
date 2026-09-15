# API — referência de endpoints

Superfície completa da API do Flow na Sprint 2: **54 endpoints**, todos sob `/api/v1`.

A especificação OpenAPI é gerada a partir da aplicação em execução e exportada em
`dist/presentation-assets/openapi.json` pelo script de entrega, então ela reflete a tabela
de rotas real e não uma cópia mantida à mão.

- **Swagger UI:** `/swagger` (Development, ou com `Swagger__Enabled=true`)
- **Autenticação:** `Authorization: Bearer <access token>`
- **Erros:** RFC 7807 `application/problem+json`, sempre com `traceId`

---

## 1. Convenções

### Códigos de status

| Código | Quando |
|---|---|
| `200` | Sucesso com corpo |
| `201` | Recurso criado, com `Location` |
| `204` | Sucesso sem corpo |
| `401` | Falha de autenticação: sem token, token inválido ou expirado, credencial errada, refresh revogado |
| `403` | Autenticado, mas sem permissão — por papel **ou** por recurso |
| `404` | Recurso inexistente |
| `409` | Conflito de estado: transição inválida, duplicidade, regra de domínio |
| `422` | Entrada inválida, com `errors` por campo |
| `429` | Rate limit excedido |
| `503` | Dependência externa indisponível (assistente) |

`422` para entrada malformada e `409` para conflito de estado é uma mudança consciente em
relação à Sprint 1, que devolvia `400` nos dois casos. O cliente móvel precisa distinguir
"corrija este campo" de "esta ação não é possível agora".

`401` para credencial errada e para refresh recusado também é uma correção em relação à
Sprint 1, que devolvia `403`. `401` é "não sei quem você é"; `403` é "sei, e não pode". O
app reage de formas diferentes a cada um: um manda para o login, o outro explica que a ação
não é sua. Colapsar os dois faz uma senha errada parecer falta de permissão.

**Não existe `400` neste contrato.** Falha de binding — JSON malformado, tipo errado —
também sai como `422`, no mesmo formato e com `traceId`. Duas formas de recusar a mesma
classe de problema seriam dois contratos para o cliente tratar.

### Formato de erro

```json
{
  "type": "https://httpstatuses.io/422",
  "title": "Validation failed",
  "status": 422,
  "instance": "/api/v1/ideas",
  "errors": { "Title": ["'título' deve ser informado."] },
  "traceId": "0af7651916cd43dd8448eb211c80319c"
}
```

O `traceId` é o mesmo valor gravado em `audit_logs.correlationId` e no trace distribuído,
então um erro relatado pelo usuário pode ser encontrado no log, no trace e na trilha de
auditoria a partir de uma única string.

### Qual campo o usuário lê

| Campo | Para quem | Idioma |
|---|---|---|
| `title` | Desenvolvedor e suporte | Inglês — é a mensagem da exceção |
| `errors` | Usuário, ao lado do campo | **pt-BR**, regra e nome do campo |
| `userMessage` | Usuário, quando existe | **pt-BR** |
| `traceId` | Suporte | — |

`title` carrega a mensagem da exceção, escrita para quem lê o código e o log, e é assim que
ela deve continuar: o domínio fala a língua do código. O texto que o usuário lê é
responsabilidade do cliente, que tem a cópia em pt-BR por tipo de erro — e o app **não**
ecoa `title`.

A exceção é `userMessage`: a API só o preenche quando a mensagem foi escrita **para o
usuário**. Hoje isso acontece apenas nas recusas do assistente ("O assistente está
temporariamente indisponível. O restante do Flow segue funcionando."), que explicam algo que
uma frase genérica não explicaria.

As mensagens de validação são caso à parte porque vão para `errors` e são exibidas ao lado
do campo: elas são localizadas **no servidor**, incluindo o nome do campo — a cultura do
FluentValidation é pt-BR e os rótulos vêm de `FieldLabels`.

### Papéis

| Papel | Significado |
|---|---|
| `Operator` | Submete ideias e acompanha as suas |
| `Manager` | Avalia ideias, decide e conduz projetos |
| `Leadership` | Define estratégia e acompanha o resultado |

Autorização por papel é aplicada na borda HTTP. Autorização **por recurso** é aplicada no
handler, por uma policy única — `ResourceAccessPolicy`, na camada Application.

A regra, vinda da matriz de papéis da especificação:

| Papel | O que enxerga |
|---|---|
| `Manager`, `Leadership` | O programa inteiro. Avaliar e conduzir é a razão de existir dos papéis. |
| `Operator` | Só a própria trilha: as ideias que enviou, e os projetos e resultados que nasceram delas. |

"Trilha do Operator" é literal: ele acessa um projeto quando é o responsável **ou** quando o
projeto veio de uma ideia que ele enviou (`sourceIdeaId`). A listagem é **restringida na
consulta**, não filtrada depois — filtrar depois faria a paginação revelar quantos
documentos foram escondidos.

Saber o GUID nunca basta. `ResourceAuthorizationTests` cobre cada porta: ideia, comentários,
projeto, linha do tempo, snapshots, resultado financeiro e listagem.

---

## 2. Autenticação

`POST /api/v1/auth/*` — rate limit de 10 requisições por minuto por endereço.

| Método | Rota | Papel | Descrição |
|---|---|---|---|
| POST | `/auth/register` | anônimo | Cria conta. **Sempre** como `Operator`. |
| POST | `/auth/login` | anônimo | Autentica e emite o par de tokens. |
| POST | `/auth/refresh` | anônimo | Rotaciona o par. O token apresentado é revogado. |
| POST | `/auth/logout` | autenticado | Revoga o refresh token informado. |

**Rotação e replay.** Cada refresh emite um par novo e revoga o anterior. Apresentar um
token já rotacionado é a assinatura clássica de roubo: o Flow revoga **toda** a cadeia
ativa do usuário e devolve `401`.

**Armazenamento.** O refresh token nunca é persistido em claro — apenas o SHA-256. Um dump
do banco não é reproduzível contra a API.

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "manager@flow.demo", "password": "..." }
```

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "3OgLu1J+zwAKmthPUv0E...",
  "userId": "a988b41e-d5ed-4c85-851c-cde578b48ccc",
  "name": "Ana Ribeiro",
  "email": "manager@flow.demo",
  "role": "Manager"
}
```

---

## 3. Estratégia

| Método | Rota | Papel |
|---|---|---|
| GET | `/guidelines` | autenticado |
| GET | `/guidelines/current` | autenticado |
| GET | `/guidelines/{id}` | autenticado |
| GET | `/guidelines/{id}/history` | autenticado |
| POST | `/guidelines` | Leadership |
| PUT | `/guidelines/{id}` | Leadership |
| POST | `/guidelines/{id}/close` | Leadership |
| DELETE | `/guidelines/{id}` | Leadership |

Filtros em `GET /guidelines`: `category`, `campaign`, `currentOnly`.

**Vigência é derivada**, nunca um booleano mutável:

```text
vigente(t)  ⇔  validFrom <= t  ∧  (validUntil = null ∨ t <= validUntil)
```

**Encerrar vs excluir.** `close` encerra a vigência preservando os vínculos. `DELETE`
remove de vez e devolve **409** quando ideias ou projetos já referenciam a diretriz,
sugerindo o encerramento — apagar deixaria referências órfãs e apagaria o porquê de
prioridades passadas.

---

## 4. Ideias

| Método | Rota | Papel | Observação |
|---|---|---|---|
| GET | `/ideas` | autenticado | Operator só vê as próprias, independente do filtro |
| POST | `/ideas` | Operator | Nasce em `Draft` |
| GET | `/ideas/{id}` | autenticado | 403 para ideia de outro Operator |
| PUT | `/ideas/{id}` | Operator (autor) | Só em `Draft` |
| DELETE | `/ideas/{id}` | Operator (autor) | Só em `Draft` |
| POST | `/ideas/{id}/submit` | Operator (autor) | `Draft` → `UnderReview` |
| POST | `/ideas/{id}/approve` | Manager | Concede 50 pontos ao autor |
| POST | `/ideas/{id}/reject` | Manager | Justificativa **obrigatória** |
| PATCH | `/ideas/{id}/priority` | Manager | Rótulo qualitativo |
| PATCH | `/ideas/{id}/score` | Manager | Nota manual 0..100 |
| PUT | `/ideas/{id}/flow-score` | Manager | Componentes; o total é calculado |
| GET | `/ideas/{id}/comments` | autenticado | 403 para ideia de outro Operator |
| POST | `/ideas/{id}/comments` | Manager, Leadership | |
| POST | `/ideas/compare` | Manager, Leadership | 2 a 5 ideias, sem IA |

Filtros em `GET /ideas`: `submittedById`, `status`, `priority`, `linkedGuidelineId`,
`minScore`, `sortBy` (`score`, `flowScore`, `priority`), `skip`, `take`.

### Prioridade, nota e FlowScore são três coisas

| Campo | O que é | Quem define |
|---|---|---|
| `priority` | Rótulo qualitativo (`Low`/`Medium`/`High`) | Gestor |
| `score` | Nota direta 0..100, soberana | Gestor |
| `flowScore` | Recomendação calculada, explicável | Domínio, a partir dos componentes |

Nenhum sobrescreve o outro.

```http
PUT /api/v1/ideas/{id}/flow-score

{ "strategicAlignment": 6, "impact": 8, "feasibility": 7, "urgency": 9, "confidence": 7 }
```

```json
{
  "total": 63,
  "strategicAlignment": 6, "impact": 8, "feasibility": 7, "urgency": 9, "confidence": 7,
  "computedAt": "2026-09-09T18:40:00Z",
  "formulaVersion": 1
}
```

O total é sempre recalculado pelo domínio a partir dos componentes, então nenhum chamador
consegue injetar um número arbitrário. Fórmula em [`flowscore.md`](flowscore.md).

---

## 5. Projetos

| Método | Rota | Papel | Observação |
|---|---|---|---|
| GET | `/projects` | autenticado | Operator só vê o que é dele |
| POST | `/projects` | Manager | |
| POST | `/ideas/{ideaId}/convert` | Manager | |
| GET | `/projects/{id}` | autenticado | 403 para projeto que não é do Operator |
| PUT | `/projects/{id}` | Manager | |
| PATCH | `/projects/{id}/progress` | Manager | |
| PATCH | `/projects/{id}/stage` | Manager | |
| POST | `/projects/{id}/start` | Manager | |
| POST | `/projects/{id}/complete` | Manager | |
| POST | `/projects/{id}/block` | Manager | |
| POST | `/projects/{id}/unblock` | Manager | |
| POST | `/projects/{id}/cancel` | Manager | |
| GET | `/projects/{id}/timeline` | autenticado | Mesma regra do projeto |
| GET | `/projects/{id}/snapshots` | Manager, Leadership | |

Filtros em `GET /projects`: `ownerId`, `status`, `stage`, `linkedGuidelineId`, `skip`, `take`.

### Status e etapa são ortogonais

```text
status (governança)   Planned → InProgress → Completed
                          │         ├──────→ Cancelled
                          └─────────┴──────→ Blocked → InProgress | Cancelled

stage  (execução)     Discovery → Planning → Execution → Validation → Rollout
```

Um projeto `Blocked` mantém a etapa que já havia alcançado.

### Regras que a API garante

- `block` e `cancel` exigem motivo (**422** sem ele);
- progresso só chega a 100 pela conclusão (**409** se tentar direto);
- projeto `Planned` não reporta progresso antes de iniciar;
- concluído ou cancelado não pode ser editado;
- toda transição grava `AuditLog` **e** `ProjectSnapshot` na mesma transação.

---

## 6. Resultados

| Método | Rota | Papel | Observação |
|---|---|---|---|
| GET | `/projects/{projectId}/result` | autenticado | Mesma regra do projeto |
| PUT | `/projects/{projectId}/result` | Manager | |

Estimado e realizado são **grupos independentes**. Enviar apenas os campos de um grupo
deixa o outro intacto — é o que impede uma projeção de virar silenciosamente um resultado
alcançado.

```text
ROI = (Revenue + Savings - Cost) / Cost × 100      // null quando Cost é null ou zero
```

Além do financeiro, o resultado carrega `productivityGainPercent`, `timeSavedHours` e
`qualityGainPercent`: um projeto de processo pode valer muito e mover pouco a receita.

Valores monetários trafegam e são armazenados como `Decimal128` — sem perda de precisão.

---

## 7. Dashboard

| Método | Rota | Papel |
|---|---|---|
| GET | `/dashboard/summary` | Manager, Leadership |
| GET | `/dashboard/projects/{id}` | Manager, Leadership |
| GET | `/dashboard/strategies/{id}` | Manager, Leadership |
| POST | `/dashboard/insights` | Leadership |

`GET /dashboard/summary` devolve, em **uma** chamada: funil de ideias, saúde do portfólio,
resultado financeiro estimado e realizado, impacto não financeiro, bloqueados, projetos em
risco e atrasados, rankings, desempenho por diretriz e por campanha, e tendência mensal.

Os dados chegam prontos para desenhar — o cliente renderiza, não recalcula. No servidor
são quatro pipelines de agregação com `$facet`, um por coleção, mais a tendência via
`$unionWith` e o ranking com `$lookup` limitado ao top N.

---

## 8. Notificações

| Método | Rota | Papel |
|---|---|---|
| GET | `/notifications` | autenticado |
| POST | `/notifications/{id}/read` | autenticado (dono) |
| POST | `/notifications/read-all` | autenticado |

A central vive dentro do Flow e é independente do provedor de push: funciona configurado ou
não. Ler notificação de outro usuário devolve **403**, não 404 — é decisão de autorização,
não acidente.

---

## 9. Gamificação

| Método | Rota | Papel |
|---|---|---|
| GET | `/users/me/points` | Operator |
| GET | `/users/me/points/ledger` | Operator |
| GET | `/users/{id}/points` | Manager, Leadership |

Todo ponto concedido gera uma entrada no ledger apontando para o que o originou.

---

## 10. Funcionalidades inteligentes

| Método | Rota | Papel | Rate limit |
|---|---|---|---|
| POST | `/assistant/compare-ideas` | Manager, Leadership | 20 / 5 min por usuário |
| POST | `/assistant/ideas/{ideaId}/project-draft` | Manager, Leadership | 20 / 5 min por usuário |
| POST | `/dashboard/insights` | Leadership | 20 / 5 min por usuário |

**Por usuário é literal.** A cota pertence à identidade autenticada, não ao endereço, e
duas coisas precisam estar certas para isso funcionar — as duas invisíveis quando estão
erradas:

1. o rate limiter roda **depois** de `UseAuthentication`. Antes dela `http.User` ainda é
   anônimo, e a chave de partição cai no endereço;
2. o id é lido como o token realmente chega. O handler de bearer mapeia `sub` para
   `ClaimTypes.NameIdentifier` por padrão, então procurar só por `sub` devolve vazio
   **mesmo em requisição autenticada**.

Qualquer um dos dois faz todos os usuários atrás do mesmo NAT dividirem um balde só.
`RateLimitPartitioningTests` prova o comportamento em vez de conferir a ordem no
`Program.cs`.

A ordem do pipeline ficou assim, e cada passo tem motivo:

```text
UseRouting          → o limiter escolhe a política pelo atributo do endpoint
UseCors             → exigência do framework: CORS, autenticação e autorização nessa ordem
UseAuthentication   → a partir daqui http.User existe
UseRateLimiter      → antes da autorização: quem martela endpoint proibido também é contido
UseAuthorization
MapControllers
```

A política de `/auth/*` continua **por endereço**, de propósito: credential stuffing não
reusa uma conta, percorre uma lista — particionar por usuário ali não protegeria nada.

**Nenhum destes endpoints altera estado de domínio.** O rascunho de projeto volta como
preview editável; o projeto só existe quando o gestor executa
`POST /ideas/{ideaId}/convert`, passando o `assistantRunId` para fechar o ciclo de
governança.

Sem `GEMINI_API_KEY` configurada respondem **503** e todo o restante do Flow continua
funcionando. Detalhes em [`ai-integration.md`](ai-integration.md).

---

## 11. Operação

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| GET | `/health/live` | não | O processo está de pé |
| GET | `/health/ready` | não | O MongoDB está alcançável |
| GET | `/swagger/v1/swagger.json` | não | Especificação OpenAPI |

`live` e `ready` são separados de propósito: um orquestrador não deve matar um pod saudável
por causa de uma oscilação momentânea do banco.

---

## 12. Tabela completa

Gerada a partir do `openapi.json` exportado.

| Método | Rota | Grupo | Respostas documentadas |
|---|---|---|---|
| POST | /api/v1/assistant/compare-ideas | Assistant | 200,422,429,503 |
| POST | /api/v1/assistant/ideas/{ideaId}/project-draft | Assistant | 200,409,429,503 |
| POST | /api/v1/auth/login | Auth | 200 |
| POST | /api/v1/auth/logout | Auth | 200 |
| POST | /api/v1/auth/refresh | Auth | 200 |
| POST | /api/v1/auth/register | Auth | 200 |
| POST | /api/v1/dashboard/insights | Dashboard | 200,429,503 |
| GET | /api/v1/dashboard/projects/{id} | Dashboard | 200,404 |
| GET | /api/v1/dashboard/strategies/{id} | Dashboard | 200,404 |
| GET | /api/v1/dashboard/summary | Dashboard | 200 |
| GET | /api/v1/guidelines | Guidelines | 200 |
| POST | /api/v1/guidelines | Guidelines | 201,422 |
| GET | /api/v1/guidelines/current | Guidelines | 200 |
| DELETE | /api/v1/guidelines/{id} | Guidelines | 204,409 |
| GET | /api/v1/guidelines/{id} | Guidelines | 200,404 |
| PUT | /api/v1/guidelines/{id} | Guidelines | 204 |
| POST | /api/v1/guidelines/{id}/close | Guidelines | 204,409 |
| GET | /api/v1/guidelines/{id}/history | Guidelines | 200 |
| GET | /api/v1/ideas | Ideas | 200 |
| POST | /api/v1/ideas | Ideas | 201,422 |
| POST | /api/v1/ideas/compare | Ideas | 200 |
| POST | /api/v1/ideas/{ideaId}/convert | Projects | 201,409 |
| DELETE | /api/v1/ideas/{id} | Ideas | 204,403,409 |
| GET | /api/v1/ideas/{id} | Ideas | 200,403,404 |
| PUT | /api/v1/ideas/{id} | Ideas | 204,403,409 |
| POST | /api/v1/ideas/{id}/approve | Ideas | 204 |
| GET | /api/v1/ideas/{id}/comments | Ideas | 200 |
| POST | /api/v1/ideas/{id}/comments | Ideas | 201 |
| PUT | /api/v1/ideas/{id}/flow-score | Ideas | 200,422 |
| PATCH | /api/v1/ideas/{id}/priority | Ideas | 204 |
| POST | /api/v1/ideas/{id}/reject | Ideas | 204,422 |
| PATCH | /api/v1/ideas/{id}/score | Ideas | 204,422 |
| POST | /api/v1/ideas/{id}/submit | Ideas | 204 |
| GET | /api/v1/notifications | Notifications | 200 |
| POST | /api/v1/notifications/read-all | Notifications | 204 |
| POST | /api/v1/notifications/{id}/read | Notifications | 204,403 |
| GET | /api/v1/projects | Projects | 200 |
| POST | /api/v1/projects | Projects | 201,422 |
| GET | /api/v1/projects/{id} | Projects | 200,404 |
| PUT | /api/v1/projects/{id} | Projects | 204 |
| POST | /api/v1/projects/{id}/block | Projects | 204,422 |
| POST | /api/v1/projects/{id}/cancel | Projects | 204,422 |
| POST | /api/v1/projects/{id}/complete | Projects | 204 |
| PATCH | /api/v1/projects/{id}/progress | Projects | 204,409 |
| GET | /api/v1/projects/{id}/snapshots | Projects | 200 |
| PATCH | /api/v1/projects/{id}/stage | Projects | 204 |
| POST | /api/v1/projects/{id}/start | Projects | 204 |
| GET | /api/v1/projects/{id}/timeline | Projects | 200 |
| POST | /api/v1/projects/{id}/unblock | Projects | 204 |
| GET | /api/v1/projects/{projectId}/result | Results | 200,404 |
| PUT | /api/v1/projects/{projectId}/result | Results | 200,422 |
| GET | /api/v1/users/me/points | Users | 200 |
| GET | /api/v1/users/me/points/ledger | Users | 200 |
| GET | /api/v1/users/{id}/points | Users | 200 |
