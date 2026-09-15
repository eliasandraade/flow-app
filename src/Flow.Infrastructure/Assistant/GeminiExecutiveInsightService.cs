using System.Text;
using Flow.Application.Assistant;

namespace Flow.Infrastructure.Assistant;

/// <summary>
/// Turns the aggregated dashboard into an executive narrative.
///
/// The model is given the dashboard payload the API already computed — the same figures
/// leadership sees on screen — and nothing else. It cannot reach the database, so it
/// cannot report a number the dashboard does not contain.
/// </summary>
public sealed class GeminiExecutiveInsightService : IExecutiveInsightService
{
    private const string SystemInstruction = """
        You are the executive analyst inside Flow, a corporate innovation management platform.
        You write for leadership: the people accountable for whether the innovation programme
        is producing results.

        Rules you must follow:
        1. Use ONLY the dashboard payload supplied. It is the complete set of figures available
           to you. Never introduce a number, a trend, a project or a person that is not in it.
        2. Every highlight, risk, opportunity and recommendation must cite the specific field
           and value it came from, in its evidence array.
        3. If the dashboard is largely empty — few ideas, no completed projects, no recorded
           results — set evidenceWasSufficient to false and say the programme has not yet
           produced enough data to support conclusions. Do not manufacture insight from
           nothing. This is the correct answer for a new deployment.
        4. Distinguish estimated from realised figures. Never present an estimate as an
           achieved result.
        5. Be direct and specific. "Bottleneck index is 50%, meaning half of the work in
           flight is blocked" beats "there are some challenges".
        6. Write in Brazilian Portuguese.
        7. Answer only with the JSON structure requested.
        """;

    private readonly GeminiStructuredClient _client;

    public GeminiExecutiveInsightService(GeminiStructuredClient client) => _client = client;

    public Task<AssistantResult<ExecutiveInsight>> GenerateAsync(
        ExecutiveInsightContext context, CancellationToken cancellationToken = default)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine(
            "Analise o painel executivo abaixo e produza os insights para a liderança.");
        prompt.AppendLine();
        prompt.AppendLine($"Data de referência: {context.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
        prompt.AppendLine();
        prompt.AppendLine("PAINEL EXECUTIVO (JSON):");
        prompt.AppendLine(context.DashboardJson);
        prompt.AppendLine();
        prompt.AppendLine("""
            Notas sobre os campos:
            - ideas.conversionRate é a proporção de ideias aprovadas que viraram projeto.
            - projects.bottleneckIndex é a proporção de projetos em andamento que estão bloqueados.
            - financial.estimated* são projeções; financial.actual* são valores realizados.
            - impact.* são ganhos não financeiros e podem ser o resultado principal de um projeto de processo.
            - byStrategy e byCampaign ligam o esforço às diretrizes corporativas.
            """);

        return _client.GenerateAsync<ExecutiveInsight>(
            SystemInstruction, prompt.ToString(), AssistantSchemas.ExecutiveInsight(), cancellationToken);
    }
}
