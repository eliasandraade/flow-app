using System.Text;
using System.Text.Json;
using Flow.Application.Assistant;

namespace Flow.Infrastructure.Assistant;

/// <summary>
/// Gemini-backed copilot for the manager.
///
/// The application assembles the context from data the requesting user is already
/// authorised to see and hands it over in full. That is deliberately preferred to letting
/// the model fetch its own data through tool calls: the set of relevant records for
/// "compare these three ideas" is known up front, so a tool loop would add round trips and
/// an extra surface where the model could reach for something outside the caller's scope,
/// in exchange for nothing.
///
/// Full reasoning: docs/sprint-2/ai-integration.md
/// </summary>
public sealed class GeminiInnovationAssistant : IInnovationAssistant
{
    private const string SystemInstruction = """
        You are the innovation copilot inside Flow, a corporate innovation management platform.
        You advise a manager who is deciding which ideas to prioritise and how to turn an
        approved idea into a project.

        Rules you must follow:
        1. Use ONLY the data supplied in the user message. Never invent a metric, a cost, a
           date or a name that is not there.
        2. When the data does not support a conclusion, set evidenceWasSufficient to false and
           say plainly what is missing. An honest "not enough evidence" is a correct answer.
        3. Every claim in evidence must quote a concrete value or field from the supplied data.
        4. You advise. You never decide. Never state that an idea has been approved, rejected,
           prioritised or converted; those are decisions a human makes in the product.
        5. Write in Brazilian Portuguese, in the direct language of an operations manager.
           Keep names, titles and identifiers exactly as supplied.
        6. Answer only with the JSON structure requested.
        """;

    private static readonly JsonSerializerOptions ContextJson = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly GeminiStructuredClient _client;

    public GeminiInnovationAssistant(GeminiStructuredClient client) => _client = client;

    public Task<AssistantResult<IdeaComparisonInsight>> CompareIdeasAsync(
        IdeaComparisonContext context, CancellationToken cancellationToken = default)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("Compare as ideias abaixo e ajude o gestor a priorizar.");
        prompt.AppendLine();

        if (!string.IsNullOrWhiteSpace(context.Question))
        {
            prompt.AppendLine("Pergunta específica do gestor:");
            prompt.AppendLine(context.Question.Trim());
            prompt.AppendLine();
        }

        prompt.AppendLine("DIRETRIZES ESTRATÉGICAS VIGENTES (JSON):");
        prompt.AppendLine(JsonSerializer.Serialize(context.CurrentGuidelines, ContextJson));
        prompt.AppendLine();
        prompt.AppendLine("IDEIAS EM ANÁLISE (JSON):");
        prompt.AppendLine(JsonSerializer.Serialize(context.Ideas, ContextJson));
        prompt.AppendLine();
        prompt.AppendLine(
            "O FlowScore é calculado pelo próprio Flow a partir dos componentes mostrados "
            + "(alinhamento estratégico, impacto, viabilidade, urgência e confiança). "
            + "Use-o como evidência, não o recalcule.");

        return _client.GenerateAsync<IdeaComparisonInsight>(
            SystemInstruction, prompt.ToString(), AssistantSchemas.IdeaComparison(), cancellationToken);
    }

    public Task<AssistantResult<ProjectDraft>> DraftProjectAsync(
        ProjectDraftContext context, CancellationToken cancellationToken = default)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine(
            "Proponha um rascunho de projeto para a ideia aprovada abaixo. "
            + "O rascunho será apresentado ao gestor como um preview editável: ele revisa, "
            + "ajusta e só então cria o projeto no Flow.");
        prompt.AppendLine();
        prompt.AppendLine("IDEIA APROVADA (JSON):");
        prompt.AppendLine(JsonSerializer.Serialize(context.Idea, ContextJson));
        prompt.AppendLine();
        prompt.AppendLine("DIRETRIZES ESTRATÉGICAS VIGENTES (JSON):");
        prompt.AppendLine(JsonSerializer.Serialize(context.CurrentGuidelines, ContextJson));
        prompt.AppendLine();

        if (context.ComparableOutcomes.Count > 0)
        {
            prompt.AppendLine("PROJETOS ANTERIORES COMPARÁVEIS E SEUS RESULTADOS REAIS (JSON):");
            prompt.AppendLine(JsonSerializer.Serialize(context.ComparableOutcomes, ContextJson));
            prompt.AppendLine();
            prompt.AppendLine(
                "Baseie duração e custo estimados nesses resultados reais. "
                + "Se eles não sustentarem um número, devolva 0 e explique na justificativa.");
        }
        else
        {
            prompt.AppendLine(
                "Não há projetos anteriores comparáveis. Devolva 0 em duração e custo estimados "
                + "e registre em evidence que não havia base histórica.");
        }

        return _client.GenerateAsync<ProjectDraft>(
            SystemInstruction, prompt.ToString(), AssistantSchemas.ProjectDraft(), cancellationToken);
    }
}
