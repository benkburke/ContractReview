using System.Text.Json;
using ContractReview.Domain.Errors;

namespace ContractReview.Domain.Claims;

/// <summary>
/// The second-pass review of an evaluated claim (spec "Second-pass evaluator"). Completed carries the reviewer's verdict,
/// Failed carries only an error code. The factories enforce the output limits: confidence 0 to 100, explanation up
/// to 2,000 characters, up to 20 items per list, each up to 300 characters.
/// </summary>
public sealed record ClaimReview
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ClaimReview(
        ReviewState state,
        bool? agreesWithPrimary,
        bool? isCovered,
        int? confidence,
        string? accuracyExplanation,
        IReadOnlyList<string> coveredItems,
        IReadOnlyList<string> notCoveredItems,
        string? model,
        string? errorCode)
    {
        State = state;
        AgreesWithPrimary = agreesWithPrimary;
        IsCovered = isCovered;
        Confidence = confidence;
        AccuracyExplanation = accuracyExplanation;
        CoveredItems = coveredItems;
        NotCoveredItems = notCoveredItems;
        Model = model;
        ErrorCode = errorCode;
    }

    public ReviewState State { get; }

    public bool? AgreesWithPrimary { get; }

    public bool? IsCovered { get; }

    public int? Confidence { get; }

    public string? AccuracyExplanation { get; }

    public IReadOnlyList<string> CoveredItems { get; }

    public IReadOnlyList<string> NotCoveredItems { get; }

    public string? Model { get; }

    public string? ErrorCode { get; }

    public static ClaimReview Completed(
        bool agreesWithPrimary,
        bool isCovered,
        int confidence,
        string accuracyExplanation,
        IReadOnlyList<string> coveredItems,
        IReadOnlyList<string> notCoveredItems,
        string model)
    {
        if (confidence is < ClaimRules.MinConfidence or > ClaimRules.MaxConfidence
            || string.IsNullOrWhiteSpace(accuracyExplanation)
            || accuracyExplanation.Length > ClaimRules.MaxReviewExplanationLength
            || !AreItemsValid(coveredItems)
            || !AreItemsValid(notCoveredItems)
            || string.IsNullOrWhiteSpace(model)
            || model.Length > ClaimRules.MaxModelLength)
        {
            throw new DomainException(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
        }

        return new ClaimReview(
            ReviewState.Completed,
            agreesWithPrimary,
            isCovered,
            confidence,
            accuracyExplanation,
            coveredItems,
            notCoveredItems,
            model,
            null);
    }

    public static ClaimReview Failed(string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > ClaimRules.MaxErrorCodeLength)
        {
            throw new DomainException(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
        }

        return new ClaimReview(ReviewState.Failed, null, null, null, null, [], [], null, errorCode);
    }

    /// <summary>Serializes the explanation and lists, stored in the single JSON column.</summary>
    public string ToDetailsJson() =>
        JsonSerializer.Serialize(new StoredDetails(AccuracyExplanation, CoveredItems, NotCoveredItems), Json);

    /// <summary>Rebuilds a review from its stored columns; null when the state column is empty (not reviewed).</summary>
    public static ClaimReview? FromStored(
        ReviewState? state,
        bool? agrees,
        bool? isCovered,
        int? confidence,
        string? model,
        string? errorCode,
        string? detailsJson)
    {
        if (state is null)
        {
            return null;
        }

        if (state == ReviewState.Failed)
        {
            return Failed(errorCode ?? ErrorCodes.ReviewFailed);
        }

        var details = detailsJson is null ? null : JsonSerializer.Deserialize<StoredDetails>(detailsJson, Json);
        if (details is null || agrees is null || isCovered is null || confidence is null || model is null)
        {
            throw new DomainException(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
        }

        return Completed(
            agrees.Value,
            isCovered.Value,
            confidence.Value,
            details.AccuracyExplanation ?? string.Empty,
            details.CoveredItems,
            details.NotCoveredItems,
            model);
    }

    private static bool AreItemsValid(IReadOnlyList<string> items) =>
        items.Count <= ClaimRules.MaxReviewItems
        && items.All(i => !string.IsNullOrWhiteSpace(i) && i.Length <= ClaimRules.MaxItemLength);

    private sealed record StoredDetails(
        string? AccuracyExplanation,
        IReadOnlyList<string> CoveredItems,
        IReadOnlyList<string> NotCoveredItems);
}
