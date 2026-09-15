# FlowScore — priorização explicável de ideias

O FlowScore é o mecanismo de priorização do Flow. Ele produz uma nota **0..100** para
cada ideia, com componentes visíveis e fórmula pública, de modo que um gestor consiga
responder à pergunta que realmente importa numa reunião de portfólio:

> *"Por que a ideia A está acima da ideia B?"*

---

## 1. Princípios

1. **Determinístico.** A mesma entrada produz sempre a mesma saída. Não depende de rede,
   de modelo de linguagem nem de horário.
2. **Explicável.** Cada componente e sua contribuição ponderada são persistidos e
   exibidos. Nada de número mágico.
3. **A entrada manual é soberana.** O gestor define os componentes. As funcionalidades
   inteligentes podem *sugerir* valores; nunca gravá-los.
4. **Independente do Gemini.** Se o provedor de IA estiver indisponível, a priorização
   continua funcionando integralmente.
5. **Distinto de `Priority` e de `Score`.** São três conceitos:
   - `Priority` — rótulo qualitativo do gestor (`Low`/`Medium`/`High`);
   - `Score` — nota manual direta do gestor (0..100), soberana;
   - `FlowScore` — recomendação calculada a partir dos componentes.

---

## 2. Fundamentação

O modelo combina três frameworks consagrados, mantendo de cada um a ideia que se aplica
ao contexto de inovação corporativa e descartando o que não se aplica.

| Framework | O que foi aproveitado | O que foi descartado e por quê |
|---|---|---|
| **RICE** (Intercom) | Confiança como **multiplicador que desconta** uma estimativa otimista. | *Reach* como contagem de usuários: uma ideia de chão de fábrica não tem "alcance" comparável ao de uma feature de produto. A divisão direta por esforço também foi descartada (ver §4). |
| **WSJF** (SAFe) | Separar **urgência/criticidade de tempo** do valor de negócio, em vez de fundir tudo em "importância". | Divisão por duração do job: no Flow o esforço entra como dimensão ponderada, não como divisor. |
| **Weighted Scoring / Strategic Alignment** | Pesos explícitos por dimensão, com **alinhamento estratégico como maior peso**. | Escalas livres por avaliador, que impedem comparação entre ideias. |

O peso maior do alinhamento estratégico não é arbitrário: a tese do produto é conectar
ideia a diretriz corporativa. Uma ideia excelente e desalinhada é, para este produto,
menos valiosa que uma boa ideia alinhada.

---

## 3. Componentes

Cada componente é um inteiro **0..10**.

| Componente | Símbolo | Peso | Significado | Origem |
|---|---|---|---|---|
| Alinhamento estratégico | `A` | **0,35** | Aderência a uma diretriz vigente. | Derivado do vínculo, ajustável pelo gestor. |
| Impacto | `I` | **0,25** | Tamanho do resultado esperado. | Gestor. |
| Viabilidade | `F` | **0,25** | Facilidade de execução. `10` = trivial, `0` = enorme. | Gestor. |
| Urgência | `U` | **0,15** | Criticidade temporal. | Gestor. |
| Confiança | `C` | multiplicador | Qualidade da evidência por trás das estimativas. | Gestor. |

Soma dos pesos aditivos: `0,35 + 0,25 + 0,25 + 0,15 = 1,00`.

**Viabilidade é o inverso do esforço**, declarado assim de propósito: todas as dimensões
aditivas passam a ter a mesma direção ("maior é melhor"), o que elimina a principal fonte
de erro de leitura em modelos de scoring.

### Derivação de `A`

| Situação | `A` inicial |
|---|---|
| Vinculada a diretriz **vigente** na data da avaliação | 7 |
| Vinculada a diretriz **fora de vigência** | 3 |
| Sem diretriz vinculada | 2 |

Valor inicial, não final: o gestor sobrepõe quando conhece o contexto.

---

## 4. Fórmula

```text
Base = 0,35·A + 0,25·I + 0,25·F + 0,15·U                    ∈ [0, 10]

FatorConfiança = 0,6 + 0,4·(C / 10)                         ∈ [0,6 , 1,0]

FlowScore = arredondar( 10 · Base · FatorConfiança )         ∈ [0, 100]
```

### Arredondamento

`arredondar` é **meio para cima** (`MidpointRounding.AwayFromZero`), não o padrão do
.NET. `Math.Round` usa arredondamento bancário (`ToEven`) por padrão, e a diferença é
observável: o caso "Desalinhada, fácil e certa" da tabela abaixo produz exatamente
`50,50`, que vira `51` com meio-para-cima e `50` com arredondamento bancário. O modo é
fixado no código e coberto por teste para que a nota não dependa de um detalhe da
biblioteca padrão.

### Por que a confiança multiplica em vez de somar

Somar confiança permitiria que uma ideia sem nenhuma evidência subisse no ranking apenas
por ser "confiantemente medíocre". Multiplicar preserva a intuição do RICE: a confiança
**desconta** uma estimativa, nunca a cria.

### Por que o piso do multiplicador é 0,6 e não 0

Confiança zero zeraria a nota de uma ideia estrategicamente vital cujo impacto ainda não
foi medido — exatamente o tipo de ideia que uma plataforma de inovação deveria fazer
subir para investigação. O piso de 0,6 preserva até 60% da nota e deixa a confiança
decidir empates, que é o papel correto dela.

### Por que não dividir por esforço, como no RICE

Divisão direta é instável na faixa baixa: com esforço 1 a nota explode e com esforço 10
ela colapsa, e a diferença entre esforço 1 e 2 vira maior que entre 5 e 10. Como
viabilidade entra ponderada, a curva fica monótona e legível, sem descontinuidade.

---

## 5. Exemplos verificáveis

Estes casos são exatamente os fixados nos testes automatizados.

| Caso | A | I | F | U | C | Base | Fator | **FlowScore** |
|---|---|---|---|---|---|---|---|---|
| Máximo | 10 | 10 | 10 | 10 | 10 | 10,00 | 1,00 | **100** |
| Mínimo | 0 | 0 | 0 | 0 | 0 | 0,00 | 0,60 | **0** |
| Neutro | 5 | 5 | 5 | 5 | 5 | 5,00 | 0,80 | **40** |
| Alinhada, cara e incerta | 10 | 8 | 2 | 5 | 3 | 6,75 | 0,72 | **49** |
| Desalinhada, fácil e certa | 2 | 6 | 9 | 4 | 10 | 5,05 | 1,00 | **51** |
| Estratégica sem evidência | 10 | 9 | 6 | 8 | 0 | 8,45 | 0,60 | **51** |

O terceiro e o quarto caso mostram o modelo funcionando: uma ideia muito alinhada porém
cara e incerta empata praticamente com uma ideia desalinhada, barata e comprovada. É uma
conversa de portfólio legítima, não um veredito escondido.

### Conferência do caso "Alinhada, cara e incerta"

```text
Base   = 0,35·10 + 0,25·8 + 0,25·2 + 0,15·5
       = 3,50 + 2,00 + 0,50 + 0,75 = 6,75
Fator  = 0,6 + 0,4·(3/10) = 0,72
Score  = arredondar(10 · 6,75 · 0,72) = arredondar(48,6) = 49
```

---

## 6. Propriedades garantidas por teste

| # | Propriedade |
|---|---|
| P1 | O resultado está sempre em `[0, 100]`. |
| P2 | É monotônico não decrescente em `A`, `I`, `F`, `U` e `C`, mantidos os demais fixos. |
| P3 | Entradas fora de `[0, 10]` são rejeitadas por invariante de domínio, não silenciosamente truncadas. |
| P4 | Duas avaliações com a mesma entrada produzem a mesma saída (determinismo). |
| P5 | Os componentes persistidos reproduzem exatamente o total: recalcular a partir deles devolve o mesmo número. |
| P6 | O cálculo não realiza nenhuma chamada de rede. |
| P7 | `formulaVersion` acompanha o resultado, permitindo interpretar notas antigas após uma futura mudança de pesos. |

---

## 7. Persistência

O resultado é gravado junto da ideia com todos os componentes, para que a explicação
sobreviva à mudança de pesos:

```jsonc
"flowScore": {
  "total": 49,
  "components": {
    "strategicAlignment": 10, "impact": 8,
    "feasibility": 2, "urgency": 5, "confidence": 3
  },
  "computedAt": ISODate("2026-09-09T18:40:00Z"),
  "formulaVersion": 1
}
```

Alterar pesos no futuro exige **incrementar `formulaVersion`**. Notas antigas continuam
explicáveis pelos componentes que as geraram.

---

## 8. Relação com as funcionalidades inteligentes

O Copiloto do Gestor pode **sugerir** valores de componentes com justificativa textual.
A sugestão chega ao aplicativo como um preview editável.

O FlowScore só muda quando o gestor confirma, e a confirmação percorre o comando normal
da Application: autorização, validação, domínio e auditoria. O modelo nunca escreve
diretamente, e a indisponibilidade dele não afeta a priorização em nada.
