using ContractReview.Domain.Claims;

namespace ContractReview.Application.Abstractions.Chat;

/// <summary>
/// The model's answer before it is checked against the retrieved chunks. <see cref="CitedLabels"/> are the chunk labels
/// the model cited; the handler keeps only labels that were retrieved.
/// </summary>
public sealed record LlmClaimAnswer(
    ClaimStatus Status,
    int Confidence,
    string? CoverageLevel,
    bool NeedsCoverageLevel,
    IReadOnlyList<string> CoverageLevelOptions,
    IReadOnlyList<string> CoveredItems,
    IReadOnlyList<string> ExcludedItems,
    string Reasoning,
    string? FollowUpQuestion,
    IReadOnlyList<string> CitedLabels)
{
    public const string ViolationStatus = "status is not a known value";
    public const string ViolationConfidence = "confidence is outside 0 to 100";
    public const string ViolationCoverageLevel = "coverage level is too long";
    public const string ViolationCoveredItems = "too many or invalid covered items";
    public const string ViolationExcludedItems = "too many or invalid excluded items";
    public const string ViolationCitations = "too many citations";
    public const string ViolationOptions = "too many or invalid coverage level options";
    public const string ViolationReasoning = "reasoning is too long";
    public const string ViolationFollowUp = "follow-up question is too long";
    public const string ViolationNeedsLevel = "needsCoverageLevel is set without options";

    /// <summary>Returns what breaks the output limits (spec "Validation rules and limits"), or null when the answer is valid.</summary>
    public string? FindViolation()
    {
        if (!Enum.IsDefined(Status))
        {
            return ViolationStatus;
        }

        if (Confidence is < ClaimRules.MinConfidence or > ClaimRules.MaxConfidence)
        {
            return ViolationConfidence;
        }

        if (CoverageLevel is not null && CoverageLevel.Length > ClaimRules.MaxCoverageLevelLength)
        {
            return ViolationCoverageLevel;
        }

        if (!AreItemsValid(CoveredItems, ClaimRules.MaxCoveredItems))
        {
            return ViolationCoveredItems;
        }

        if (!AreItemsValid(ExcludedItems, ClaimRules.MaxExcludedItems))
        {
            return ViolationExcludedItems;
        }

        if (!AreItemsValid(CoverageLevelOptions, ClaimRules.MaxCoverageLevelOptions))
        {
            return ViolationOptions;
        }

        if (CitedLabels.Count > ClaimRules.MaxCitations)
        {
            return ViolationCitations;
        }

        if (Reasoning.Length > ClaimRules.MaxReasoningLength)
        {
            return ViolationReasoning;
        }

        if (FollowUpQuestion is not null && FollowUpQuestion.Length > ClaimRules.MaxFollowUpQuestionLength)
        {
            return ViolationFollowUp;
        }

        return NeedsCoverageLevel && CoverageLevelOptions.Count == 0 ? ViolationNeedsLevel : null;
    }

    private static bool AreItemsValid(IReadOnlyList<string> items, int maxCount) =>
        items.Count <= maxCount && items.All(i => !string.IsNullOrWhiteSpace(i) && i.Length <= ClaimRules.MaxItemLength);
}
