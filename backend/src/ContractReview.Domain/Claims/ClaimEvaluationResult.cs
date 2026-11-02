using System.Text.Json;
using ContractReview.Domain.Errors;

namespace ContractReview.Domain.Claims;

/// <summary>
/// The structured outcome of evaluating a claim. The constructor enforces the invariants: a valid status, confidence
/// 0 to 100, a decisive status (Covered, NotCovered, Conditional) only with at least one citation, and
/// <see cref="NeedsCoverageLevel"/> only with at least one option. <see cref="Create"/> is the factory that downgrades a
/// decisive answer without citations to <see cref="ClaimStatus.InsufficientInformation"/> (D-032).
/// </summary>
public sealed record ClaimEvaluationResult
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ClaimEvaluationResult(
        ClaimStatus status,
        int confidence,
        string? coverageLevel,
        bool needsCoverageLevel,
        IReadOnlyList<string> coverageLevelOptions,
        IReadOnlyList<string> coveredItems,
        IReadOnlyList<string> excludedItems,
        string reasoning,
        string? followUpQuestion,
        IReadOnlyList<ClaimCitation> citations,
        string model)
    {
        if (!Enum.IsDefined(status)
            || confidence is < ClaimRules.MinConfidence or > ClaimRules.MaxConfidence
            || (coverageLevel is not null && coverageLevel.Length > ClaimRules.MaxCoverageLevelLength)
            || coverageLevelOptions.Count > ClaimRules.MaxCoverageLevelOptions
            || coveredItems.Count > ClaimRules.MaxCoveredItems
            || excludedItems.Count > ClaimRules.MaxExcludedItems
            || citations.Count > ClaimRules.MaxCitations
            || !coverageLevelOptions.Concat(coveredItems).Concat(excludedItems).All(IsValidItem)
            || reasoning is null
            || reasoning.Length > ClaimRules.MaxReasoningLength
            || (followUpQuestion is not null && followUpQuestion.Length > ClaimRules.MaxFollowUpQuestionLength)
            || citations.Any(c => string.IsNullOrWhiteSpace(c.SectionHeading)
                || c.Excerpt is null
                || c.Excerpt.Length > ClaimRules.MaxExcerptLength)
            || string.IsNullOrWhiteSpace(model)
            || model.Length > ClaimRules.MaxModelLength
            || (IsDecisive(status) && citations.Count == 0)
            || (needsCoverageLevel && coverageLevelOptions.Count == 0))
        {
            throw new DomainException(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
        }

        Status = status;
        Confidence = confidence;
        CoverageLevel = coverageLevel;
        NeedsCoverageLevel = needsCoverageLevel;
        CoverageLevelOptions = coverageLevelOptions;
        CoveredItems = coveredItems;
        ExcludedItems = excludedItems;
        Reasoning = reasoning;
        FollowUpQuestion = followUpQuestion;
        Citations = citations;
        Model = model;
    }

    public ClaimStatus Status { get; }

    public int Confidence { get; }

    /// <summary>The level used or inferred from the contract; null when unknown.</summary>
    public string? CoverageLevel { get; }

    public bool NeedsCoverageLevel { get; }

    public IReadOnlyList<string> CoverageLevelOptions { get; }

    public IReadOnlyList<string> CoveredItems { get; }

    public IReadOnlyList<string> ExcludedItems { get; }

    public string Reasoning { get; }

    public string? FollowUpQuestion { get; }

    public IReadOnlyList<ClaimCitation> Citations { get; }

    public string Model { get; }

    /// <summary>True for Covered, NotCovered and Conditional: answers that must be backed by a citation.</summary>
    public static bool IsDecisive(ClaimStatus status) =>
        status is ClaimStatus.Covered or ClaimStatus.NotCovered or ClaimStatus.Conditional;

    /// <summary>Builds a result, downgrading a decisive status without any citation to InsufficientInformation (D-032).</summary>
    public static ClaimEvaluationResult Create(
        ClaimStatus status,
        int confidence,
        string? coverageLevel,
        bool needsCoverageLevel,
        IReadOnlyList<string> coverageLevelOptions,
        IReadOnlyList<string> coveredItems,
        IReadOnlyList<string> excludedItems,
        string reasoning,
        string? followUpQuestion,
        IReadOnlyList<ClaimCitation> citations,
        string model)
    {
        if (IsDecisive(status) && citations.Count == 0)
        {
            status = ClaimStatus.InsufficientInformation;
        }

        return new ClaimEvaluationResult(
            status,
            confidence,
            coverageLevel,
            needsCoverageLevel,
            coverageLevelOptions,
            coveredItems,
            excludedItems,
            reasoning,
            followUpQuestion,
            citations,
            model);
    }

    /// <summary>Serializes the fields that are stored in the single JSON column (the rest have their own columns).</summary>
    public string ToDetailsJson() =>
        JsonSerializer.Serialize(
            new StoredDetails(CoveredItems, ExcludedItems, CoverageLevelOptions, Reasoning, FollowUpQuestion, Citations),
            Json);

    /// <summary>Rebuilds a result from its stored columns and details JSON.</summary>
    public static ClaimEvaluationResult FromStored(
        ClaimStatus status,
        int confidence,
        string? coverageLevel,
        bool needsCoverageLevel,
        string model,
        string detailsJson)
    {
        var details = JsonSerializer.Deserialize<StoredDetails>(detailsJson, Json)
            ?? throw new DomainException(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
        return new ClaimEvaluationResult(
            status,
            confidence,
            coverageLevel,
            needsCoverageLevel,
            details.CoverageLevelOptions,
            details.CoveredItems,
            details.ExcludedItems,
            details.Reasoning,
            details.FollowUpQuestion,
            details.Citations,
            model);
    }

    private static bool IsValidItem(string item) =>
        !string.IsNullOrWhiteSpace(item) && item.Length <= ClaimRules.MaxItemLength;

    private sealed record StoredDetails(
        IReadOnlyList<string> CoveredItems,
        IReadOnlyList<string> ExcludedItems,
        IReadOnlyList<string> CoverageLevelOptions,
        string Reasoning,
        string? FollowUpQuestion,
        IReadOnlyList<ClaimCitation> Citations);
}
