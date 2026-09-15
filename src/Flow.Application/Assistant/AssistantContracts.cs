namespace Flow.Application.Assistant;

/// <summary>
/// Contextual copilot for the manager.
///
/// Deliberately not a chat box: each capability is a defined product operation with a
/// typed result, so the answer can be rendered, audited and acted on rather than merely
/// read. The provider only ever receives data the requesting user is already allowed to
/// see, assembled by the application, and it never writes anything.
/// </summary>
public interface IInnovationAssistant
{
    Task<AssistantResult<IdeaComparisonInsight>> CompareIdeasAsync(
        IdeaComparisonContext context, CancellationToken cancellationToken = default);

    Task<AssistantResult<ProjectDraft>> DraftProjectAsync(
        ProjectDraftContext context, CancellationToken cancellationToken = default);
}

/// <summary>Executive narrative built strictly from the aggregated dashboard figures.</summary>
public interface IExecutiveInsightService
{
    Task<AssistantResult<ExecutiveInsight>> GenerateAsync(
        ExecutiveInsightContext context, CancellationToken cancellationToken = default);
}

// ─── Outcome envelope ───────────────────────────────────────────────────────

public enum AssistantOutcomeKind
{
    Success,

    /// <summary>The provider answered, but not with something we could parse.</summary>
    MalformedResponse,

    Timeout,

    /// <summary>Provider unreachable, rate limited, or the circuit is open.</summary>
    Unavailable,

    /// <summary>No API key configured. The rest of the product is unaffected.</summary>
    NotConfigured
}

/// <summary>
/// Every call returns an outcome rather than throwing, because an unavailable model is an
/// expected state for this product, not an exception. The core pipeline keeps working
/// without it.
/// </summary>
public sealed record AssistantResult<T>(
    AssistantOutcomeKind Outcome,
    T? Value,
    string Model,
    long LatencyMs,
    int? PromptTokens = null,
    int? ResponseTokens = null,
    string? Error = null)
{
    public bool IsSuccess => Outcome == AssistantOutcomeKind.Success && Value is not null;
}

// ─── Context sent to the provider ───────────────────────────────────────────

public sealed record AssistantIdea(
    Guid Id,
    string Title,
    string Problem,
    string Description,
    string Status,
    string Priority,
    int? Score,
    int? FlowScore,
    IReadOnlyDictionary<string, int>? FlowScoreComponents,
    string? LinkedGuidelineTitle,
    bool GuidelineIsCurrent,
    int CommentCount,
    int DaysUnderReview);

public sealed record AssistantGuideline(
    Guid Id, string Title, string Category, string? Campaign, bool IsCurrent);

public sealed record IdeaComparisonContext(
    IReadOnlyList<AssistantIdea> Ideas,
    IReadOnlyList<AssistantGuideline> CurrentGuidelines,
    string? Question);

public sealed record ProjectDraftContext(
    AssistantIdea Idea,
    IReadOnlyList<AssistantGuideline> CurrentGuidelines,
    IReadOnlyList<HistoricalProjectOutcome> ComparableOutcomes);

public sealed record HistoricalProjectOutcome(
    string Title,
    string Status,
    int DurationDays,
    decimal? ActualNetValue,
    decimal? ActualRoi);

public sealed record ExecutiveInsightContext(
    string DashboardJson,
    DateTimeOffset GeneratedAt);

// ─── Structured results ─────────────────────────────────────────────────────

public sealed record IdeaAssessment(
    Guid IdeaId,
    string Title,
    string StrategicFit,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Risks,
    string Recommendation);

public sealed record IdeaComparisonInsight(
    string Summary,
    IReadOnlyList<IdeaAssessment> Assessments,
    IReadOnlyList<string> TradeOffs,
    Guid? SuggestedPriorityIdeaId,
    string SuggestedPriorityRationale,
    IReadOnlyList<string> Evidence,
    bool EvidenceWasSufficient);

/// <summary>
/// A proposal, never a project. It reaches the manager as an editable preview and only
/// becomes real through the ordinary application command, with authorisation, validation,
/// domain rules and an audit entry.
/// </summary>
public sealed record ProjectDraft(
    string Title,
    string Description,
    string SuggestedPriority,
    string SuggestedStage,
    int? SuggestedDurationDays,
    decimal? SuggestedEstimatedCost,
    IReadOnlyList<string> Milestones,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> SuccessCriteria,
    string Rationale,
    IReadOnlyList<string> Evidence,
    bool EvidenceWasSufficient);

public sealed record InsightItem(string Title, string Detail, IReadOnlyList<string> Evidence);

public sealed record ExecutiveInsight(
    string ExecutiveSummary,
    IReadOnlyList<InsightItem> Highlights,
    IReadOnlyList<InsightItem> Risks,
    IReadOnlyList<InsightItem> Opportunities,
    IReadOnlyList<InsightItem> Recommendations,
    IReadOnlyList<string> Evidence,
    bool EvidenceWasSufficient,
    DateTimeOffset GeneratedAt);
