using ContractReview.Domain.Claims;

namespace ContractReview.Application.Features.Claims.Evaluation;

/// <summary>The reviewer model's verdict before it is stored as a <see cref="ClaimReview"/>.</summary>
public sealed record LlmClaimReview(
    bool AgreesWithPrimary,
    bool IsCovered,
    int Confidence,
    string AccuracyExplanation,
    IReadOnlyList<string> CoveredItems,
    IReadOnlyList<string> NotCoveredItems)
{
    public const string ViolationConfidence = "confidence is outside 0 to 100";
    public const string ViolationExplanation = "accuracy explanation is empty or too long";
    public const string ViolationCoveredItems = "too many or invalid covered items";
    public const string ViolationNotCoveredItems = "too many or invalid not covered items";

    /// <summary>Returns what breaks the output limits (spec "Validation rules and limits"), or null when the review is valid.</summary>
    public string? FindViolation()
    {
        if (Confidence is < ClaimRules.MinConfidence or > ClaimRules.MaxConfidence)
        {
            return ViolationConfidence;
        }

        if (string.IsNullOrWhiteSpace(AccuracyExplanation) || AccuracyExplanation.Length > ClaimRules.MaxReviewExplanationLength)
        {
            return ViolationExplanation;
        }

        if (!AreItemsValid(CoveredItems))
        {
            return ViolationCoveredItems;
        }

        return AreItemsValid(NotCoveredItems) ? null : ViolationNotCoveredItems;
    }

    private static bool AreItemsValid(IReadOnlyList<string> items) =>
        items.Count <= ClaimRules.MaxReviewItems
        && items.All(i => !string.IsNullOrWhiteSpace(i) && i.Length <= ClaimRules.MaxItemLength);
}
