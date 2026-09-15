# Roteiro de demonstração

Um percurso de 12 a 15 minutos que conta uma história de inovação corporativa de ponta a
ponta, e não um passeio por telas.

**A história:** uma operadora industrial define uma diretriz para reduzir retrabalho. Uma
operadora de chão de fábrica propõe manutenção preditiva. A gestora avalia com critério
transparente, aprova e transforma em projeto. O projeto entrega, o resultado é medido, e
a liderança vê o retorno ligado à diretriz que o originou.

---

## 0. Preparação

```bash
cp .env.example .env
# preencha JWT_SECRET_KEY, SEED_DEMO_PASSWORD e, se tiver, GEMINI_API_KEY
docker compose up -d
```

Ou, sem Docker:

```bash
# MongoDB precisa ser um replica set — transações não existem em standalone
mongod --replSet rs0 --dbpath ./data --port 27017
mongosh --eval 'rs.initiate({_id:"rs0", members:[{_id:0, host:"127.0.0.1:27017"}]})'

export SEED_DEMO_DATA=true
export SEED_DEMO_PASSWORD='FlowDemo!2026'
dotnet run --project src/Flow.API
```

### Contas de demonstração

| Papel | E-mail |
|---|---|
| Operador | `operator@flow.demo` |
| Operador | `bruno@flow.demo` |
| Gestora | `manager@flow.demo` |
| Liderança | `leadership@flow.demo` |

A senha é a de `SEED_DEMO_PASSWORD`. **Não é um segredo de produção**: existe apenas para
o ambiente de demonstração e é definida por variável de ambiente, nunca commitada.

### O que o seed cria

5 diretrizes em 3 campanhas (uma já encerrada), 10 ideias em todos os estados, 6 projetos
— entregues, em execução, bloqueado, planejado e cancelado —, resultados com ROI e ganhos
não financeiros, pontos, comentários, notificações e trilha de auditoria com datas
distribuídas ao longo de 6 meses.

O seed é idempotente: rodar de novo não duplica nada.

---

## 1. Arquitetura (2 min)

Antes de abrir o app, o desenho.

```text
Mobile (Expo)  →  Flow.API  →  Flow.Application  →  Flow.Domain
                                      ↑
                             Flow.Infrastructure
                          MongoDB · Gemini · OneSignal
```

Pontos a destacar:

1. **As setas apontam para dentro.** O domínio não conhece MongoDB, HTTP nem SDK nenhum.
2. **MongoDB em replica set**, não por gosto: agregado, auditoria e snapshot precisam
   commitar juntos, e transação multi-documento não existe em standalone.
3. **Identity sobre Mongo escrito no projeto**, apenas os seis contratos que o produto usa.

Mostrar o Swagger em `/swagger`: **54 endpoints** documentados com método, rota, payload,
respostas e ProblemDetails.

---

## 2. Operador — de um problema real a uma ideia (2 min)

Entrar como `operator@flow.demo`.

1. **Home.** A tela abre pela **estratégia vigente**, não por um formulário vazio. Dizer
   ao operador o que a empresa está buscando antes de pedir uma ideia é o que faz a ideia
   nascer alinhada.
2. **Nova ideia.** O primeiro campo é **o problema**, não o título — é a informação que
   realmente importa e a que se perde quando o formulário começa pela solução.
3. Vincular à diretriz *"Reduzir o retrabalho na linha de montagem"*.
4. **Minhas ideias.** Filtrar por situação. Abrir uma ideia aprovada e mostrar o retorno
   do gestor e os **pontos** ganhos.
5. **Avisos.** A central de notificações mostra o que aconteceu com cada ideia.

> Enquanto for rascunho, a ideia pode ser editada e excluída. Depois de enviada, não —
> e a tela diz isso antes, não depois.

---

## 3. Gestora — decidir com critério visível (4 min)

Entrar como `manager@flow.demo`.

1. **Fila de ideias**, já ordenada por FlowScore. Responde a pergunta que a tela existe
   para responder: o que eu olho primeiro.

2. **Avaliar uma ideia.** Preencher as cinco dimensões — alinhamento estratégico, impacto,
   viabilidade, urgência e confiança — e calcular o FlowScore.

   **Este é o momento central da demonstração.** Mostrar que o app exibe cada dimensão com
   seu peso, não um número solto:

   ```text
   Base  = 0,35·Alinhamento + 0,25·Impacto + 0,25·Viabilidade + 0,15·Urgência
   Fator = 0,6 + 0,4·(Confiança/10)
   Score = arredondar(10 · Base · Fator)
   ```

   Com 6, 8, 7, 9, 7 o resultado é **63**. Confiança **multiplica** em vez de somar, seguindo
   o RICE: ela desconta uma estimativa otimista, nunca cria valor do nada. O piso de 0,6
   impede que uma ideia estrategicamente vital seja zerada por ainda não ter medição.

3. **Comparar.** Selecionar duas ou três ideias e comparar lado a lado — sem IA, direto dos
   dados. Funciona offline e nunca discorda da fila.

4. **Copiloto.** Agora sim a camada inteligente. Ele devolve avaliação por ideia, riscos,
   trade-offs e uma recomendação, **com a evidência de cada conclusão**.

   Dizer com todas as letras: *ele recomenda, quem decide é o gestor.*

5. **Aprovar.** A justificativa é registrada, o autor é notificado e ganha 50 pontos.

6. **Rascunho de projeto pelo copiloto.** Ele propõe título, marcos, riscos, critérios de
   sucesso e — quando há histórico comparável — duração e custo ancorados em **resultados
   reais de projetos anteriores**.

   A tela diz "Rascunho, não projeto". **Nada foi criado.** Editar um campo e criar. Só
   então o projeto existe, pelo comando normal, com auditoria e snapshot.

7. **Conduzir o projeto.** Iniciar, avançar a etapa, atualizar o progresso, bloquear com
   motivo. Mostrar que **etapa e situação são ortogonais**: um projeto bloqueado continua
   na etapa que alcançou.

8. **Governança.** Abrir a linha do tempo: cada transição, com anterior → novo, motivo e
   autor. Depois a aba de snapshots: o estado **completo** capturado em cada transição.

   > A auditoria diz o que mudou. O snapshot diz como o projeto era.

9. **Resultado.** Registrar estimado e realizado — em blocos separados, de propósito, para
   que uma projeção nunca vire silenciosamente um resultado alcançado. Registrar também
   produtividade, horas economizadas e qualidade: um projeto de processo pode valer muito
   e mover pouco a receita.

---

## 4. Liderança — o retorno do programa (4 min)

Entrar como `leadership@flow.demo`.

1. **Painel executivo.** Uma única chamada traz tudo. O cliente desenha, não recalcula.

   Percorrer na ordem em que a tela foi montada:

   - **Resultado** — valor realizado, ROI, horas economizadas, produtividade;
   - **O que está travando** — índice de gargalo, bloqueados com há quantos dias, atrasados
     e em risco, cada um clicável direto para o projeto;
   - **Funil** — taxa de aprovação e conversão em projeto;
   - **Portfólio** — distribuição por situação e por etapa;
   - **Tendência** — ideias, projetos e conclusões ao longo de 6 meses;
   - **Por diretriz e por campanha** — o elo entre esforço e estratégia;
   - **Rankings** — melhores ideias e projetos de maior retorno.

   Destacar o **índice de gargalo**: é a proporção do trabalho **em andamento** que está
   parado. Contar sobre o total incluiria projetos concluídos e diluiria o problema
   exatamente quando ele importa.

2. **Estratégia.** CRUD completo, filtro por categoria e vigência, e **histórico** de
   alterações. Mostrar a diretriz de 2025, encerrada: a vigência é **derivada do período**,
   não um interruptor — não existe estado onde a data diz uma coisa e a flag diz outra.

3. **Insights executivos.** Resumo, destaques, riscos, oportunidades e recomendações, cada
   item citando o campo e o valor de onde saiu.

   Se o programa tiver poucos dados, ele **diz que não há evidência suficiente** em vez de
   inventar análise. Essa é a resposta certa para uma instalação nova.

---

## 5. O que sustenta tudo isso (3 min)

Voltar ao terminal.

### A garantia transacional

```bash
dotnet test --filter "FullyQualifiedName~TransactionalIntegrity"
```

Explicar o que o teste faz: injeta uma falha na escrita de auditoria no meio da aprovação
de uma ideia e verifica que **o status não mudou, os pontos não foram creditados e a
notificação não foi criada**.

> Com EF Core essa atomicidade vinha de graça de um único SaveChanges. Em MongoDB ela
> precisa ser construída de propósito — e é exatamente o tipo de garantia que some em
> silêncio durante uma migração se nada a provar.

### A suíte

```bash
dotnet test
```

**313 testes**, sendo 170 de integração contra um MongoDB real e descartável, com replica
set e transações reais. Nenhum fake nos fluxos críticos.

### Degradação

```bash
# sem GEMINI_API_KEY os endpoints inteligentes respondem 503
curl -s -X POST localhost:5153/api/v1/dashboard/insights -H "Authorization: Bearer $TOKEN" | jq
```

E então mostrar o painel e a fila funcionando normalmente. O **FlowScore continua sendo
calculado**, porque é determinístico e vive no domínio: a priorização nunca fica refém de
uma rede.

### Observabilidade

```bash
curl -s localhost:5153/health/live  | jq
curl -s localhost:5153/health/ready | jq
```

Provocar um erro e mostrar que o `traceId` do ProblemDetails é o mesmo do log estruturado
e o mesmo gravado em `audit_logs.correlationId`.

---

## 6. Fechamento

Uma frase para cada coisa que o Flow prova:

1. **Rastreabilidade real** — diretriz → ideia → projeto → resultado, com auditoria
   append-only e snapshot imutável em cada transição.
2. **Priorização explicável** — o FlowScore mostra por que A está acima de B, e não depende
   de IA nenhuma.
3. **IA com governança** — aconselha, nunca decide, registra toda execução e diz quando não
   tem evidência.
4. **Degradação previsível** — Gemini fora, OneSignal fora, o produto continua de pé.
5. **Verificado, não afirmado** — 313 testes, incluindo o rollback transacional.

---

## Se algo der errado ao vivo

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| API não sobe | Mongo não é replica set | `rs.initiate(...)`, conferir com `rs.status()` |
| Login falha | `SEED_DEMO_PASSWORD` diferente do usado no seed | Recriar o banco e semear de novo |
| Endpoints de IA em 503 | Sem `GEMINI_API_KEY` | É o comportamento correto — use para demonstrar a degradação |
| Push não chega | Sem credencial OneSignal | A central no app funciona; as mensagens ficam pendentes, e isso é proposital |
| Painel vazio | `SEED_DEMO_DATA` não estava ligado | Ligar e reiniciar |
