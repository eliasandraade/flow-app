using FluentValidation;

namespace Flow.Application.Common.Validation;

/// <summary>
/// pt-BR labels for the fields that appear in validation messages.
///
/// FluentValidation builds a message like "'Title' deve ser informado" from the property
/// name, and the user reads that text verbatim next to the field. Translating the rules
/// but leaving the field names in English produces a half-translated interface, so the
/// display name is resolved from here instead.
///
/// One dictionary rather than a <c>WithName</c> on every rule: the same field means the
/// same thing everywhere in the product, and a rule added later is covered without anyone
/// remembering to name it. Anything absent falls back to the property name.
/// </summary>
public static class FieldLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Name"] = "nome",
        ["Email"] = "e-mail",
        ["Password"] = "senha",
        ["AccessToken"] = "token de acesso",
        ["RefreshToken"] = "token de renovação",

        ["Title"] = "título",
        ["Description"] = "descrição",
        ["Problem"] = "problema",
        ["Body"] = "comentário",
        ["Notes"] = "observações",
        ["Reason"] = "motivo",
        ["ManagerComment"] = "parecer do gestor",

        ["Category"] = "categoria",
        ["Campaign"] = "campanha",
        ["ValidUntil"] = "data final",

        ["Score"] = "nota",
        ["StrategicAlignment"] = "alinhamento estratégico",
        ["Impact"] = "impacto",
        ["Feasibility"] = "viabilidade",
        ["Urgency"] = "urgência",
        ["Confidence"] = "confiança",

        ["OwnerId"] = "responsável",
        ["ProgressPercentage"] = "progresso",

        ["EstimatedCost"] = "custo estimado",
        ["EstimatedRevenue"] = "receita estimada",
        ["EstimatedSavings"] = "economia estimada",
        ["ActualCost"] = "custo realizado",
        ["ActualRevenue"] = "receita realizada",
        ["ActualSavings"] = "economia realizada",
        ["TimeSavedHours"] = "horas economizadas",
        ["ProductivityGainPercent"] = "ganho de produtividade",
        ["QualityGainPercent"] = "ganho de qualidade",
        ["PaybackPeriodMonths"] = "prazo de retorno"
    };

    /// <summary>
    /// Wired into <see cref="ValidatorOptions.Global"/> so every rule in the assembly gets
    /// the label without declaring it.
    /// </summary>
    public static string? Resolve(Type? _, System.Reflection.MemberInfo? member, System.Linq.Expressions.LambdaExpression? __)
        => member is not null && Labels.TryGetValue(member.Name, out var label) ? label : null;
}
