using System.ComponentModel.DataAnnotations;

namespace ContractReview.Application.Options;

public sealed class ReviewOptions
{
    public const string SectionName = "Review";
    public const int DefaultTimeoutSeconds = 45;

    /// <summary>When false, no second-pass review runs and claims have no review.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Overall time limit of one review, including its retry; a timeout counts as a review failure.</summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;
}
