# Integração com Gemini — funcionalidades inteligentes do produto

Este documento descreve a integração do **produto Flow** com o Google Gemini: o Copiloto
do Gestor e os Insights Executivos.

- **Provedor:** Google Gemini
- **Modelo:** `gemini-3.8-flash`
- **SDK:** `Google.GenAI` 1.21.0 (SDK oficial do Google para .NET, suporta `net8.0`)
- **Chave:** `GEMINI_API_KEY`, exclusivamente server-side

---

## 1. Princípio que governa tudo

> A inteligência **aconselha**. Ela nunca decide, nunca escreve no banco e nunca contorna
> autorização ou regra de domínio.

Nenhum endpoint sob `/api/v1/assistant` altera estado de domínio. O caminho de uma
sugestão até virar realidade é sempre o mesmo:

```text
1. o serviço gera uma sugestão estruturada
2. o app apresenta um preview editável
3. o gestor revisa e confirma
4. um comando normal da Application executa
5. autorização, validação e regras de domínio se aplicam
6. auditoria e snapshot são produzidos
```

O teste `ProjectDraft_CreatesNothingUntilAHumanConfirmsIt` prova essa propriedade: ele
conta os projetos antes e depois de pedir um rascunho e confirma que nada foi criado.

---

## 2. Capacidades

Não é uma caixa de texto genérica. Cada capacidade é uma operação de produto com
resultado tipado, que pode ser renderizado, auditado e transformado em ação.

| Capacidade | Endpoint | Papel | Retorno |
|---|---|---|---|
| Comparar ideias | `POST /api/v1/assistant/compare-ideas` | Manager, Leadership | `IdeaComparisonInsight` |
| Rascunho de projeto | `POST /api/v1/assistant/ideas/{id}/project-draft` | Manager, Leadership | `ProjectDraft` |
| Insights executivos | `POST /api/v1/dashboard/insights` | Leadership | `ExecutiveInsight` |

### 2.1 Copiloto do Gestor — comparação de ideias

Responde às perguntas que o gestor realmente faz na reunião de portfólio:

- qual dessas ideias tem maior alinhamento estratégico;
- quais são os riscos de cada uma;
- quais são os trade-offs entre elas;
- qual eu priorizaria e por quê.

Recebe as ideias com o **detalhamento completo do FlowScore**, as diretrizes vigentes,
a contagem de comentários e há quantos dias cada ideia está parada em análise. A
justificativa devolvida cita os mesmos números que a tela mostra.

### 2.2 Copiloto do Gestor — rascunho de projeto

A partir de uma ideia **aprovada**, propõe título, descrição, prioridade, etapa, duração,
custo estimado, marcos, riscos e critérios de sucesso.

Duração e custo são ancorados em **resultados reais de projetos anteriores comparáveis** —
preferindo os da mesma diretriz estratégica. Quando não há histórico, a instrução obriga a
devolver `0` e registrar em `evidence` que não havia base, em vez de inventar um número.

### 2.3 Insights Executivos

Transforma o painel agregado em narrativa: resumo executivo, destaques, riscos,
oportunidades e recomendações, cada item citando os campos e valores de onde saiu.

O modelo recebe **exatamente o payload do dashboard** que a liderança vê na tela, e nada
além disso. Ele não tem acesso ao banco, logo não consegue reportar um número que o
dashboard não contém.

---

## 3. Decisão arquitetural: injeção de contexto em vez de function calling

O brief permite function calling "quando útil". A escolha aqui foi **injeção de contexto
no servidor**, e ela é deliberada.

| Critério | Function calling | Injeção de contexto (escolhido) |
|---|---|---|
| Escopo de dados | O modelo pede o que quiser dentro das ferramentas expostas | A aplicação decide exatamente o que enviar |
| Autorização | Cada ferramenta precisa reimplementar a checagem do chamador | O contexto já é montado a partir do que o usuário pode ver |
| Round trips | N chamadas até o modelo parar de pedir | 1 |
| Testabilidade | Depende do laço de ferramentas do provedor | Determinística |
| Custo | Cresce com o número de idas | Fixo |

Para as três capacidades acima, o conjunto de dados relevante é **conhecido de antemão**:
"compare estas três ideias" não tem ambiguidade sobre o que precisa ser carregado. Um laço
de ferramentas adicionaria latência e uma superfície a mais onde o modelo poderia pedir
algo fora do escopo do chamador, em troca de nada.

Function calling passaria a valer a pena se o produto ganhasse uma pergunta aberta do tipo
"o que está travando a Onda Operacional?", em que o próprio modelo precisa escolher o
caminho de investigação. O contrato `IInnovationAssistant` já isola isso: acrescentar essa
capacidade é acrescentar um método e uma implementação, sem tocar em nada mais.

---

## 4. Structured output

Toda chamada exige JSON contra um schema explícito
(`GenerateContentConfig.ResponseJsonSchema` + `ResponseMimeType = "application/json"`).
Resposta em texto livre é tratada como **falha**, não como algo a exibir.

A ordem das propriedades no schema é intencional: o modelo gera os campos na ordem
declarada, então resumo e evidência vêm depois do raciocínio que os produziu.

### Como a proibição de inventar métrica é implementada

Todo schema carrega dois campos:

```jsonc
"evidence": ["string"],          // valores e campos concretos usados
"evidenceWasSufficient": true    // false quando os dados não sustentam a conclusão
```

A instrução de sistema é explícita: quando o painel está vazio ou os dados não sustentam
uma conclusão, a resposta correta é `evidenceWasSufficient: false` com a explicação do que
falta. Para uma instalação nova do Flow, essa é a resposta **certa**.

---

## 5. Resiliência

| Controle | Decisão |
|---|---|
| Timeout | 30s por chamada, configurável, propagado por `CancellationToken` |
| Retry | **Nenhum**, deliberadamente |
| Circuit breaker | Abre após 5 falhas consecutivas, cooldown de 1 minuto, meia-abertura com uma requisição de teste |
| Degradação | Falha do Gemini nunca afeta o núcleo do produto |

### Por que não há retry

Geração de conteúdo é um **POST não idempotente e cobrado**. Repetir depois de um timeout
paga duas vezes por uma resposta que pode já estar a caminho, e pode duplicar efeito.
Timeout e circuit breaker cobrem os modos de falha que importam; retry só adicionaria
custo.

Isso contrasta com o OneSignal, onde o retry **é** aplicado: lá a entrega é idempotente
pela `dedupeKey`, então repetir é seguro.

### Degradação verificada

O teste `WhenTheAssistantIsDown_TheRestOfTheProductKeepsWorking` derruba o provedor e
confirma que o pipeline inteiro continua: ideia criada, submetida, pontuada com FlowScore,
aprovada, e o dashboard respondendo normalmente.

**O FlowScore não depende do Gemini.** Ele é determinístico e calculado pelo domínio
(ver [`flowscore.md`](flowscore.md)). A priorização nunca fica refém de uma rede.

---

## 6. Governança

Toda execução grava um documento em `assistant_runs`:

| Campo | Conteúdo |
|---|---|
| `userId`, `userRole` | quem solicitou |
| `requestedAt` | quando |
| `operation` | `CompareIdeas`, `DraftProject`, `ExecutiveInsights` |
| `model` | modelo utilizado |
| `outcome` | `Success`, `Failed`, `Timeout`, `Unavailable` |
| `latencyMs` | latência |
| `promptTokens`, `responseTokens` | consumo, quando o provedor informa |
| `structuredResult` | resultado estruturado devolvido |
| `correlationId` | liga ao trace distribuído |
| `suggestionAccepted` | `true` apenas se um humano efetivou a sugestão |
| `acceptedEntityType`, `acceptedEntityId` | o que foi criado a partir dela |

Falhas também são registradas: uma chamada que deu timeout faz parte do quadro
operacional tanto quanto uma que funcionou.

O ciclo fecha quando o gestor converte a ideia passando o `assistantRunId` devolvido pelo
rascunho — aí a execução é marcada como aceita e apontada para o projeto que nasceu dela.

Isto é **governança funcional do produto**, não autoria de código.

---

## 7. Segurança

| Regra | Como é garantida |
|---|---|
| Chave só no servidor | `GEMINI_API_KEY` por variável de ambiente; o app mobile nunca a recebe |
| Chave nunca em log | Exceções do provedor não são logadas por inteiro, apenas o tipo |
| Conteúdo sensível | `assistant_runs` grava o resultado estruturado, nunca credencial ou token |
| Rate limit | 20 requisições / 5 minutos por usuário nos endpoints de IA |
| Autorização | Copiloto: Manager e Leadership. Insights: Leadership. Verificada **antes** de gastar uma chamada |
| Dados expostos ao modelo | Somente o que o usuário autenticado já pode ver |

O teste `Copilot_IsNotAvailableToOperators` confirma que a autorização roda antes da
chamada ao provedor: um Operator recebe 403 e o provedor não é acionado.

---

## 8. Observabilidade

| Sinal | Onde |
|---|---|
| `flow_ai_requests` | contador por operação |
| `flow_ai_failures` | contador por operação e tipo de falha |
| `flow_ai_latency_ms` | histograma por operação |
| Trace | `Flow` ActivitySource, correlacionado com `assistant_runs.correlationId` |

Nenhuma métrica leva `userId` como label: cardinalidade alta quebra o backend de métricas.
A atribuição por usuário vive em `assistant_runs` e nos traces.

---

## 9. Configuração

```bash
GEMINI_API_KEY=<sua chave>        # sem isto, os endpoints respondem 503 e o resto funciona
Gemini__Model=gemini-3.8-flash
Gemini__Timeout=00:00:30
Gemini__Temperature=0.2           # baixa: isto é análise de números reais, não redação criativa
```

Sem chave configurada, `GeminiStructuredClient.IsConfigured` é `false`, os endpoints
devolvem **503** com mensagem clara, a execução ainda é registrada em `assistant_runs`
como `Unavailable`, e **todo o restante do Flow segue funcionando**.

---

## 10. Estado de verificação

| Aspecto | Estado |
|---|---|
| Contrato e tipos neutros na Application | ✅ verificado por teste |
| Structured output e parsing | ✅ verificado por teste |
| Resposta malformada do provedor | ✅ verificado por teste |
| Timeout | ✅ verificado por teste |
| Provedor indisponível | ✅ verificado por teste |
| Circuit breaker (abre, meia-abertura, fecha) | ✅ verificado por teste |
| Autorização por papel | ✅ verificado por teste |
| Ausência de mutação automática | ✅ verificado por teste |
| Registro de governança e aceite | ✅ verificado por teste |
| Degradação sem afetar o núcleo | ✅ verificado por teste |
| **Chamada real ao `gemini-3.8-flash`** | ⏳ **pendente de credencial** |

O último item é honesto e deliberado: não há `GEMINI_API_KEY` no ambiente de
desenvolvimento, então a chamada ao provedor real **não foi executada**. Tudo o que
depende do nosso código está verificado; o que falta é exclusivamente a validação com
credencial.

Para executá-la:

```bash
export GEMINI_API_KEY=<chave>
dotnet run --project src/Flow.API
# autentique como leadership e chame POST /api/v1/dashboard/insights
```

O `assistant_runs` correspondente deve registrar `outcome: Success` e o modelo
`gemini-3.8-flash`.
