using System.Text.Json.Serialization;

namespace ContractReview.Application.Features.Claims;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimReviewState
{
    Completed,
    Failed,
}

public sealed record ClaimReviewDto(
    ClaimReviewState State,
    bool? AgreesWithPrimary,
    bool? IsCovered,
    int? Confidence,
    string? AccuracyExplanation,
    IReadOnlyList<string> CoveredItems,
    IReadOnlyList<string> NotCoveredItems,
    string? Model,
    string? ErrorCode);
