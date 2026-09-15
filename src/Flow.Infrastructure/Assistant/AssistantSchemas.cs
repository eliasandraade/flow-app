using System.Text.Json.Nodes;

namespace Flow.Infrastructure.Assistant;

/// <summary>
/// JSON schemas the model must answer against.
///
/// Property order is meaningful: the model generates fields in the order they are
/// declared, so summaries and evidence are placed where the reasoning that produced them
/// has already been written out.
///
/// Every schema carries an evidence array and an evidenceWasSufficient flag. That is the
/// mechanism behind the rule that the assistant may not invent a figure: if the context
/// does not support a claim, it has somewhere to say so instead of filling the gap.
/// </summary>
internal static class AssistantSchemas
{
    private static JsonObject StringArray(string description) => new()
    {
        ["type"] = "array",
        ["description"] = description,
        ["items"] = new JsonObject { ["type"] = "string" }
    };

    private static JsonObject Text(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    private static JsonObject InsightItems(string description) => new()
    {
        ["type"] = "array",
        ["description"] = description,
        ["items"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["title"] = Text("Short headline, at most 10 words."),
                ["detail"] = Text("One or two sentences explaining it."),
                ["evidence"] = StringArray(
                    "Exact figures or field names from the supplied data that support this item.")
            },
            ["required"] = new JsonArray("title", "detail", "evidence")
        }
    };

    public static JsonNode IdeaComparison() => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["summary"] = Text("Two or three sentences comparing the ideas overall."),
            ["assessments"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "One entry per idea supplied, in the same order.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["ideaId"] = Text("The id exactly as supplied."),
                        ["title"] = Text("The idea title exactly as supplied."),
                        ["strategicFit"] = Text(
                            "How well it fits the current guidelines, grounded in the supplied data."),
                        ["strengths"] = StringArray("Concrete strengths."),
                        ["risks"] = StringArray("Concrete risks or unknowns."),
                        ["recommendation"] = Text("A short recommendation for this idea.")
                    },
                    ["required"] = new JsonArray(
                        "ideaId", "title", "strategicFit", "strengths", "risks", "recommendation")
                }
            },
            ["tradeOffs"] = StringArray("Explicit trade-offs between the ideas."),
            ["suggestedPriorityIdeaId"] = Text(
                "Id of the idea you would prioritise, or an empty string if the data does not support a choice."),
            ["suggestedPriorityRationale"] = Text("Why that idea, referring to the supplied figures."),
            ["evidence"] = StringArray("The specific data points used to reach these conclusions."),
            ["evidenceWasSufficient"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] =
                    "False when the supplied data was not enough to answer properly. Say so rather than guessing."
            }
        },
        ["required"] = new JsonArray(
            "summary", "assessments", "tradeOffs", "suggestedPriorityIdeaId",
            "suggestedPriorityRationale", "evidence", "evidenceWasSufficient")
    };

    public static JsonNode ProjectDraft() => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = Text("Project title, at most 120 characters."),
            ["description"] = Text("What the project will actually do, 2 to 4 sentences."),
            ["suggestedPriority"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "One of: Low, Medium, High, Critical.",
                ["enum"] = new JsonArray("Low", "Medium", "High", "Critical")
            },
            ["suggestedStage"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "One of: Discovery, Planning, Execution, Validation, Rollout.",
                ["enum"] = new JsonArray("Discovery", "Planning", "Execution", "Validation", "Rollout")
            },
            ["suggestedDurationDays"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Realistic duration in days, or 0 if the data does not support an estimate."
            },
            ["suggestedEstimatedCost"] = new JsonObject
            {
                ["type"] = "number",
                ["description"] =
                    "Estimated cost in the currency of the historical outcomes supplied, or 0 when there is no basis for a number. Never invent a figure."
            },
            ["milestones"] = StringArray("Three to six delivery milestones, in order."),
            ["risks"] = StringArray("Risks specific to this project."),
            ["successCriteria"] = StringArray("How success will be measured."),
            ["rationale"] = Text("Why this shape of project answers the stated problem."),
            ["evidence"] = StringArray("Data points from the idea and the historical outcomes used."),
            ["evidenceWasSufficient"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "False when there was not enough information for a grounded draft."
            }
        },
        ["required"] = new JsonArray(
            "title", "description", "suggestedPriority", "suggestedStage",
            "suggestedDurationDays", "suggestedEstimatedCost", "milestones", "risks",
            "successCriteria", "rationale", "evidence", "evidenceWasSufficient")
    };

    public static JsonNode ExecutiveInsight() => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["executiveSummary"] = Text(
                "Three to five sentences an executive can read on a phone. Plain language, real numbers."),
            ["highlights"] = InsightItems("What is going well, with the figures that show it."),
            ["risks"] = InsightItems("What is going wrong or about to."),
            ["opportunities"] = InsightItems("Where there is unrealised value."),
            ["recommendations"] = InsightItems("Specific actions, each tied to a figure."),
            ["evidence"] = StringArray("The dashboard fields and values you relied on."),
            ["evidenceWasSufficient"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] =
                    "False when the dashboard is too empty to support conclusions. An almost-empty dashboard must produce this rather than invented insight."
            }
        },
        ["required"] = new JsonArray(
            "executiveSummary", "highlights", "risks", "opportunities",
            "recommendations", "evidence", "evidenceWasSufficient")
    };
}
