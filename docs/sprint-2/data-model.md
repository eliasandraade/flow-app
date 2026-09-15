# Modelo de dados — MongoDB

Modelo documental do Flow na Sprint 2. O desenho parte dos **access patterns** reais da
aplicação, não de uma tradução mecânica do esquema relacional anterior.

- **Driver:** `MongoDB.Driver` 3.11.1 (oficial). Não é usado provider EF sobre Mongo.
- **Banco padrão:** `flow`
- **Topologia:** replica set (`rs0`). Obrigatório para transações multi-documento.
- **Escrita transacional:** ver [`architecture.md`](architecture.md), seção Unit of Work.

---

## 1. Princípios do desenho

1. **Access pattern primeiro.** Cada coleção existe para servir consultas conhecidas.
2. **Não embedar histórico ilimitado.** Comentários, snapshots, histórico de diretriz e
   auditoria crescem sem limite e ficam em coleções próprias.
3. **Agregado pequeno e coeso.** O documento principal carrega apenas o que é lido junto
   com ele na maioria das consultas.
4. **Referência por `Guid`.** Sem `$lookup` em caminho quente; o dashboard resolve nomes
   por lote (`$in`), nunca em laço.
5. **Append-only é físico, não convenção.** `audit_logs` e `project_snapshots` são expostos
   por repositórios que só sabem inserir e ler.
6. **Índice explícito.** Nenhum padrão de consulta previsto depende de collection scan.

---

## 2. Convenções de serialização

| Tipo .NET | Representação BSON | Motivo |
|---|---|---|
| `Guid` | `BsonType.String` (formato padrão) | Legível em `mongosh`, estável entre drivers e seguro para deep link. |
| `decimal` | `BsonType.Decimal128` | Valores financeiros. `double` perderia precisão em ROI, receita e custo. |
| `DateTimeOffset` | `BsonType.DateTime` (UTC) | Permite range query e operadores de data na aggregation pipeline. |
| `enum` | `BsonType.String` | Documento autoexplicativo e imune a reordenação do enum. |

### Trade-off registrado de `DateTimeOffset`

`BsonType.DateTime` guarda milissegundos em UTC. O domínio já usa `DateTimeOffset.UtcNow`
em todas as escritas, então o offset é sempre zero e nada de semântico se perde — mas a
precisão abaixo de milissegundo é truncada no round-trip.

A alternativa (`BsonType.Document`, preservando ticks e offset) foi descartada porque
tornaria toda consulta por data e toda pipeline de agregação dependente de subcampo
(`campo.DateTime`), penalizando o item mais frequente do dashboard. Os testes de
round-trip comparam com tolerância de milissegundo em vez de igualdade exata.

---

## 3. Coleções

Legenda dos índices: `U` único, `TTL` expiração automática, `C` composto.

---

### 3.1 `users`

Documento do usuário, compatível com os stores de ASP.NET Core Identity implementados
sobre Mongo.

```jsonc
{
  "_id": "6f1c…",              // Guid (string)
  "name": "Ana Ribeiro",
  "email": "ana@flow.demo",
  "normalizedEmail": "ANA@FLOW.DEMO",
  "userName": "ana@flow.demo",
  "normalizedUserName": "ANA@FLOW.DEMO",
  "passwordHash": "AQAAAA…",
  "securityStamp": "…",
  "concurrencyStamp": "…",
  "role": "Manager",            // enum de negócio
  "roles": ["Manager"],         // papéis do Identity
  "points": 350,
  "createdAt": ISODate("…"),
  "updatedAt": ISODate("…")
}
```

**Access patterns**

| # | Consulta | Uso |
|---|---|---|
| U1 | por `normalizedEmail` | login, `FindByEmailAsync` |
| U2 | por `_id` | resolução de ator e dono |
| U3 | por `_id` `$in [...]` | dashboard e snapshots resolvem nomes em lote |
| U4 | por `roles` | escolher dono de projeto |
| U5 | por `normalizedUserName` | `FindByNameAsync` do Identity |

**Índices**

| Nome | Chave | Tipo |
|---|---|---|
| `ux_users_normalizedEmail` | `{ normalizedEmail: 1 }` | U |
| `ux_users_normalizedUserName` | `{ normalizedUserName: 1 }` | U |
| `ix_users_roles` | `{ roles: 1 }` | — |

> `roles` é desnormalizado no documento do usuário porque a única leitura relevante é
> "quais papéis este usuário tem", executada em todo login. Uma coleção de junção
> exigiria uma segunda consulta em um caminho quente sem nenhum ganho.

---

### 3.2 `roles`

Catálogo pequeno e estável (`Operator`, `Manager`, `Leadership`).

```jsonc
{ "_id": "…", "name": "Manager", "normalizedName": "MANAGER" }
```

| Nome | Chave | Tipo |
|---|---|---|
| `ux_roles_normalizedName` | `{ normalizedName: 1 }` | U |

---

### 3.3 `refresh_tokens`

```jsonc
{
  "_id": "…",
  "userId": "…",
  "tokenHash": "9f2b…",        // SHA-256 do token; o valor em claro nunca é persistido
  "expiresAt": ISODate("…"),
  "createdAt": ISODate("…"),
  "revokedAt": null,
  "replacedByTokenHash": null   // cadeia de rotação
}
```

**Access patterns:** por `tokenHash` (refresh); por `userId` (revogar tudo); expiração.

| Nome | Chave | Tipo |
|---|---|---|
| `ux_refresh_tokenHash` | `{ tokenHash: 1 }` | U |
| `ix_refresh_userId` | `{ userId: 1 }` | — |
| `ttl_refresh_expiresAt` | `{ expiresAt: 1 }` | TTL |

> O TTL remove apenas tokens já expirados, o que não afeta a auditoria: a decisão de
> negócio fica em `audit_logs`, não aqui.

---

### 3.4 `strategic_guidelines`

```jsonc
{
  "_id": "…",
  "title": "Reduzir retrabalho na linha 3",
  "description": "…",
  "category": "OperationalEfficiency",
  "campaign": "Onda Operacional 2026",   // opcional
  "validFrom": ISODate("2026-01-01T00:00:00Z"),
  "validUntil": ISODate("2026-12-31T23:59:59Z"),  // null = vigência aberta
  "createdBy": "…",
  "createdAt": ISODate("…"),
  "updatedAt": ISODate("…")
}
```

**Vigência é derivada**, nunca um booleano mutável:

```text
vigente(t)  ⇔  validFrom <= t  ∧  (validUntil = null ∨ t <= validUntil)
```

**Access patterns:** vigentes em `t`; por categoria; por campanha; por `_id`; agrupamento
por categoria e campanha no dashboard.

| Nome | Chave | Tipo |
|---|---|---|
| `ix_guidelines_validity` | `{ validFrom: 1, validUntil: 1 }` | C |
| `ix_guidelines_category` | `{ category: 1, validFrom: -1 }` | C |
| `ix_guidelines_campaign` | `{ campaign: 1, validFrom: -1 }` | C |

---

### 3.5 `strategic_guideline_history`

Append-only. Uma entrada por alteração relevante.

```jsonc
{
  "_id": "…",
  "guidelineId": "…",
  "changedBy": "…",
  "changedByName": "Marcos Leal",
  "changedAt": ISODate("…"),
  "changeType": "Updated",       // Created | Updated | Closed
  "snapshot": { "title": "…", "category": "…", "validFrom": …, "validUntil": … }
}
```

| Nome | Chave | Tipo |
|---|---|---|
| `ix_guideline_history` | `{ guidelineId: 1, changedAt: -1 }` | C |

---

### 3.6 `ideas`

```jsonc
{
  "_id": "…",
  "title": "Sensor de vibração preditivo",
  "description": "…",
  "problem": "…",
  "submittedBy": "…",
  "submittedByName": "Carla Souza",
  "status": "UnderReview",
  "priority": "High",
  "score": 78,                    // 0..100, decisão do gestor
  "flowScore": {                  // cálculo determinístico, ver flowscore.md
    "total": 74,
    "components": {
      "strategicAlignment": 80, "impact": 70, "urgency": 60,
      "confidence": 75, "feasibility": 85
    },
    "computedAt": ISODate("…"),
    "formulaVersion": 1
  },
  "managerComment": "…",
  "linkedGuidelineId": "…",
  "createdAt": ISODate("…"),
  "updatedAt": ISODate("…")
}
```

`priority`, `score` e `flowScore` são **três conceitos distintos**: prioridade é o rótulo
qualitativo do gestor, `score` é a nota manual soberana e `flowScore` é a recomendação
calculada. Nenhum deles sobrescreve outro.

`submittedByName` é desnormalizado porque a fila do gestor lista dezenas de ideias e
precisaria de um `$lookup` por linha para exibir o autor. O nome do usuário é praticamente
imutável; se mudar, o histórico exibido permanece coerente com o momento da submissão.

**Access patterns:** minhas ideias (`submittedBy` + `createdAt` desc); fila por `status`
ordenada por `score`/`flowScore`/`createdAt`; por `_id`; por `linkedGuidelineId`;
contagem por `status`; top-N por score.

| Nome | Chave | Tipo |
|---|---|---|
| `ix_ideas_submittedBy` | `{ submittedBy: 1, createdAt: -1 }` | C |
| `ix_ideas_status` | `{ status: 1, createdAt: -1 }` | C |
| `ix_ideas_status_score` | `{ status: 1, score: -1 }` | C |
| `ix_ideas_priority` | `{ priority: 1 }` | — |
| `ix_ideas_guideline` | `{ linkedGuidelineId: 1 }` | — |
| `ix_ideas_createdAt` | `{ createdAt: -1 }` | — |

---

### 3.7 `idea_comments`

Coleção separada: o volume por ideia é ilimitado e a listagem é paginada.

```jsonc
{ "_id": "…", "ideaId": "…", "authorId": "…", "authorName": "…", "body": "…", "createdAt": ISODate("…") }
```

| Nome | Chave | Tipo |
|---|---|---|
| `ix_idea_comments` | `{ ideaId: 1, createdAt: 1 }` | C |

---

### 3.8 `projects`

```jsonc
{
  "_id": "…",
  "title": "Piloto de manutenção preditiva",
  "description": "…",
  "sourceIdeaId": "…",            // null quando criado manualmente
  "linkedGuidelineId": "…",       // estratégia aplicável registrada no nascimento
  "ownerId": "…",
  "ownerName": "Ana Ribeiro",
  "status": "InProgress",         // Planned | InProgress | Blocked | Completed | Cancelled
  "stage": "Execution",           // Discovery | Planning | Execution | Validation | Rollout
  "progressPercentage": 45,       // 0..100
  "priority": "High",
  "estimatedCost": NumberDecimal("120000.00"),
  "actualCost": NumberDecimal("98000.00"),
  "startDate": ISODate("…"),
  "deadline": ISODate("…"),
  "completedAt": null,
  "blockedReason": null,
  "blockedSince": null,           // preenchido na entrada em Blocked
  "cancelledReason": null,
  "createdAt": ISODate("…"),
  "updatedAt": ISODate("…")
}
```

`status` e `stage` são ortogonais: `status` é a máquina de estados de governança,
`stage` é a fase de execução. Um projeto `Blocked` continua em `Execution`.

`blockedSince` é materializado no documento. No baseline esse dado era reconstruído a
cada dashboard varrendo `project_snapshots` por `triggerAction = "Blocked"`, o que
custa uma consulta extra e um `$group` em uma coleção que só cresce.

**Access patterns:** lista por `status`, `stage`, `ownerId`; por `_id`; por
`sourceIdeaId` (conversão); por `linkedGuidelineId` (distribuição por estratégia);
`deadline` para atrasados e em risco; contagens por `status` e `stage`.

| Nome | Chave | Tipo |
|---|---|---|
| `ix_projects_status` | `{ status: 1, createdAt: -1 }` | C |
| `ix_projects_stage` | `{ stage: 1 }` | — |
| `ix_projects_owner` | `{ ownerId: 1, status: 1 }` | C |
| `ix_projects_sourceIdea` | `{ sourceIdeaId: 1 }` | — |
| `ix_projects_guideline` | `{ linkedGuidelineId: 1 }` | — |
| `ix_projects_deadline` | `{ deadline: 1, status: 1 }` | C |
| `ix_projects_progress` | `{ progressPercentage: -1 }` | — |

---

### 3.9 `project_snapshots`

Append-only e imutável. Um documento por transição de projeto.

```jsonc
{
  "_id": "…",
  "projectId": "…",
  "takenAt": ISODate("…"),
  "triggerAction": "Blocked",
  "triggeredByActorId": "…",
  "schemaVersion": 2,
  "title": "…", "description": "…", "status": "Blocked", "stage": "Execution",
  "progressPercentage": 45, "priority": "High", "ownerId": "…", "ownerName": "…",
  "sourceIdeaId": "…", "linkedGuidelineId": "…",
  "estimatedCost": NumberDecimal("…"), "actualCost": NumberDecimal("…"),
  "startDate": ISODate("…"), "deadline": ISODate("…"), "completedAt": null,
  "blockedReason": "…", "cancelledReason": null
}
```

`schemaVersion` passa de 1 para 2 nesta Sprint por causa de `stage`,
`progressPercentage` e `linkedGuidelineId`. Snapshots antigos permanecem interpretáveis.

| Nome | Chave | Tipo |
|---|---|---|
| `ix_snapshots_project` | `{ projectId: 1, takenAt: -1 }` | C |
| `ix_snapshots_trigger` | `{ projectId: 1, triggerAction: 1, takenAt: -1 }` | C |

---

### 3.10 `results`

Um resultado por projeto. Grupos estimado e real permanecem independentes.

```jsonc
{
  "_id": "…",
  "projectId": "…",
  "estimated": {
    "revenue": NumberDecimal("…"), "savings": NumberDecimal("…"),
    "cost": NumberDecimal("…"), "roi": NumberDecimal("…"),
    "recordedAt": ISODate("…")
  },
  "actual": { "revenue": …, "savings": …, "cost": …, "roi": …, "recordedAt": … },
  "paybackPeriodMonths": 14,
  "productivityGainPercent": 12.5,
  "timeSavedHours": 320.0,
  "qualityGainPercent": 8.0,
  "notes": "…",
  "recordedBy": "…",
  "createdAt": ISODate("…"), "updatedAt": ISODate("…")
}
```

| Nome | Chave | Tipo |
|---|---|---|
| `ux_results_projectId` | `{ projectId: 1 }` | U |

---

### 3.11 `point_ledger`

```jsonc
{ "_id": "…", "userId": "…", "points": 50, "reason": "Ideia aprovada",
  "referenceType": "Idea", "referenceId": "…", "awardedAt": ISODate("…") }
```

| Nome | Chave | Tipo |
|---|---|---|
| `ix_ledger_user` | `{ userId: 1, awardedAt: -1 }` | C |

---

### 3.12 `audit_logs`

Append-only. É a espinha dorsal de governança do produto.

```jsonc
{
  "_id": "…", "entityType": "Project", "entityId": "…", "action": "Blocked",
  "actorId": "…", "actorName": "…", "oldValue": "InProgress", "newValue": "Blocked",
  "reason": "Fornecedor atrasou a entrega do sensor",
  "correlationId": "0af7651916cd43dd8448eb211c80319c",
  "timestamp": ISODate("…")
}
```

`correlationId` é novo na Sprint 2 e liga a entrada de auditoria ao trace distribuído.

| Nome | Chave | Tipo |
|---|---|---|
| `ix_audit_entity` | `{ entityType: 1, entityId: 1, timestamp: -1 }` | C |
| `ix_audit_actor` | `{ actorId: 1, timestamp: -1 }` | C |
| `ix_audit_timestamp` | `{ timestamp: -1 }` | — |

---

### 3.13 `notifications`

Central de notificações do próprio Flow, independente do provedor de push.

```jsonc
{
  "_id": "…", "userId": "…", "title": "Sua ideia foi aprovada",
  "body": "…", "type": "IdeaApproved",
  "deepLink": "flow://ideas/6f1c…",
  "createdAt": ISODate("…"), "readAt": null
}
```

| Nome | Chave | Tipo |
|---|---|---|
| `ix_notifications_user` | `{ userId: 1, createdAt: -1 }` | C |
| `ix_notifications_unread` | `{ userId: 1, readAt: 1, createdAt: -1 }` | C |

---

### 3.14 `notification_outbox`

Gravada na **mesma transação** do domínio; despachada fora dela por um `HostedService`.

```jsonc
{
  "_id": "…",
  "notificationId": "…",
  "dedupeKey": "IdeaApproved:6f1c…:9a2b…",  // idempotência do despacho
  "userId": "…", "title": "…", "body": "…", "deepLink": "…",
  "status": "Pending",            // Pending | Processing | Dispatched | Failed | DeadLettered
  "attemptCount": 0,
  "nextAttemptAt": ISODate("…"),
  "lastError": null,
  "leaseOwner": null,             // quem detém o claim, só para diagnóstico
  "leaseToken": null,             // token de fencing, novo a cada claim
  "leaseExpiresAt": null,         // até quando o claim é respeitado
  "claimedAt": null,
  "createdAt": ISODate("…"), "dispatchedAt": null
}
```

| Nome | Chave | Tipo |
|---|---|---|
| `ix_outbox_dispatch` | `{ status: 1, nextAttemptAt: 1 }` | C |
| `ix_outbox_lease` | `{ status: 1, leaseExpiresAt: 1 }` | C |
| `ux_outbox_dedupe` | `{ dedupeKey: 1 }` | U |

### Claim atômico, para rodar em mais de uma réplica

Ler as mensagens devidas e só depois marcá-las não serve com duas instâncias: as duas veem
o mesmo documento `Pending` antes de qualquer uma escrever, e o destinatário recebe a
notificação duas vezes.

Por isso a seleção e a tomada de posse são **uma operação só**, um `findOneAndUpdate` por
mensagem, que escolhe uma devida e já a move para `Processing` com dono e prazo:

```text
Pending/Failed ──┐
                 ├─ claim atômico ──▶ Processing ──┬──▶ Dispatched
Processing com   │                                 ├──▶ Failed + nextAttemptAt
lease vencido ───┘                                 └──▶ DeadLettered
```

`updateMany` não serviria: o MongoDB informa quantos documentos foram tocados, não quais, e
o worker precisa saber exatamente o que passou a ser dele.

O **lease** existe para o worker que morre no meio: pod reiniciado, processo morto, deploy
durante o lote. Sem ele, a mensagem ficaria em `Processing` para sempre. Vencido o prazo,
outro worker pode retomá-la — é o segundo ramo do filtro de claim.

### Fencing: o claim precisa valer também na hora de concluir

Claim atômico impede duas réplicas de **pegarem** a mesma mensagem. Não impede a mais lenta
de **terminar**: o worker A pega, trava numa chamada ao provedor por mais tempo que o lease,
o worker B recupera legitimamente e processa, e então A acorda e grava a conclusão a que
chegou há muito tempo. Se essa escrita for endereçada só pelo `_id`, ela entra e achata o
resultado e o lease de B.

Ter `leaseOwner` no documento não resolve se a escrita final não conferir o lease. E só o
nome do worker também não basta: o mesmo worker pode ter dois claims diferentes da mesma
mensagem ao longo do tempo, e uma escrita do primeiro casaria pelo nome.

Por isso cada claim cunha um **`leaseToken` novo**, e toda mutação pós-claim é condicionada:

```text
_id == mensagem
  AND status == Processing
  AND leaseToken == token que este worker recebeu no claim
```

Sem correspondência, o worker perdeu o lease e **não sobrescreve** o estado atual — a
tentativa é descartada e registrada em log.

Nada disso depende de lock global nem de Redis. E como transação Mongo e requisição HTTP
externa não podem ser atômicas juntas, a defesa tem duas camadas: o fencing interno impede
a sobrescrita, e a chave de idempotência enviada ao provedor impede a duplicata — a mesma
mensagem leva a mesma chave em toda tentativa, inclusive quando é retomada por outro
worker.

> **Duas chaves de idempotência, de propósito.** `dedupeKey` é nossa, moldada para o nosso
> armazenamento: legível, com significado, única por evento de negócio. Já o provedor de
> push exige um UUID RFC 9562 na chave dele, e `IdeaApproved:{ideaId}:{userId}` não é um.
> Então o que viaja como `idempotency_key` é o `_id` da mensagem de outbox — já é Guid e é
> o mesmo valor em toda retentativa da mesma mensagem, que é o único jeito de a
> deduplicação do provedor funcionar.
>
> O índice único em `dedupeKey` é a garantia de idempotência: um reprocesso do mesmo
> evento falha na inserção em vez de gerar push duplicado.

---

### 3.15 `assistant_runs`

Governança das funcionalidades inteligentes. Não guarda conteúdo sensível desnecessário
e **nunca** guarda chave de API ou JWT.

```jsonc
{
  "_id": "…",
  "userId": "…",
  "userRole": "Manager",
  "operation": "CompareIdeas",   // CompareIdeas | DraftProject | ExecutiveInsights | …
  "model": "gemini-3.8-flash",
  "requestedAt": ISODate("…"),
  "latencyMs": 2840,
  "outcome": "Success",          // Success | Failed | Timeout | Unavailable
  "promptTokens": 1820, "responseTokens": 640,
  "correlationId": "…",
  "structuredResult": { … },     // resultado estruturado devolvido ao usuário
  "suggestionAccepted": false,   // vira true se a sugestão for efetivada por comando
  "acceptedEntityType": null, "acceptedEntityId": null,
  "errorKind": null
}
```

| Nome | Chave | Tipo |
|---|---|---|
| `ix_assistant_user` | `{ userId: 1, requestedAt: -1 }` | C |
| `ix_assistant_operation` | `{ operation: 1, requestedAt: -1 }` | C |

---

## 4. Rastreabilidade ponta a ponta

O dashboard consegue percorrer a cadeia completa com referências diretas, sem varredura:

```text
strategic_guidelines._id
        └── ideas.linkedGuidelineId
                 └── projects.sourceIdeaId  ──┐
        └── projects.linkedGuidelineId  ──────┤
                                              └── results.projectId
```

Toda transição do caminho aparece em `audit_logs` (`entityType` + `entityId`) e, para
projetos, também em `project_snapshots`.

---

## 5. Transações

Operações que precisam ser atômicas, executadas dentro de uma única sessão:

| Operação | Documentos afetados |
|---|---|
| Aprovar ideia | `ideas`, `users` (pontos), `point_ledger`, `audit_logs`, `notifications`, `notification_outbox` |
| Rejeitar ideia | `ideas`, `audit_logs`, `notifications`, `notification_outbox` |
| Converter ideia em projeto | `projects`, `project_snapshots`, `audit_logs`, `notifications`, `notification_outbox` |
| Transição de projeto | `projects`, `project_snapshots`, `audit_logs`, `notifications`, `notification_outbox` |
| Atualizar progresso | `projects`, `project_snapshots`, `audit_logs` |
| Registrar resultado | `results`, `audit_logs`, `notifications`, `notification_outbox` |
| Atualizar diretriz | `strategic_guidelines`, `strategic_guideline_history`, `audit_logs` |

Transação multi-documento exige replica set. Em desenvolvimento e teste usamos um
replica set de nó único; em produção, uma topologia compatível.

---

## 6. Migração a partir do esquema relacional

Não há migração de dados: a Sprint 1 não tem base de produção. O que existe é uma
**correspondência de esquema**, registrada para auditoria da mudança.

| Antes (EF Core / SQL Server) | Depois (MongoDB) | Observação |
|---|---|---|
| `AspNetUsers` + `AspNetUserRoles` + `AspNetRoles` | `users` (com `roles` embutido) + `roles` | Junção eliminada no caminho de login. |
| `RefreshTokens.Token` | `refresh_tokens.tokenHash` | Passa a guardar hash, não o valor em claro. |
| `Ideas` | `ideas` | Ganha `score` e `flowScore`. |
| `IdeaComments` | `idea_comments` | Sem mudança estrutural. |
| `Projects` | `projects` | Ganha `stage`, `progressPercentage`, `linkedGuidelineId`, `blockedSince`. |
| `ProjectSnapshots` | `project_snapshots` | `schemaVersion` 1 → 2. |
| `Results` (colunas planas) | `results` (`estimated`/`actual` aninhados) | Agrupamento explícito; ganha produtividade, horas e qualidade. |
| `StrategicGuidelines` | `strategic_guidelines` | Ganha `category`, `campaign`, `validFrom`, `validUntil`. |
| `PointLedgerEntries` | `point_ledger` | Sem mudança estrutural. |
| `AuditLogs` | `audit_logs` | Ganha `correlationId`. |
| — | `strategic_guideline_history`, `notifications`, `notification_outbox`, `assistant_runs` | Coleções novas da Sprint 2. |
| Migrations EF | Criação idempotente de índices no startup | Mongo não exige DDL prévio; o que importa é o índice existir. |
