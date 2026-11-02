using System.ComponentModel.DataAnnotations;

namespace ContractReview.Application.Options;

public sealed class ClaimsOptions
{
    public const string SectionName = "Claims";
    public const int DefaultTopK = 8;
    public const int DefaultMaxContextChars = 12_000;
    public const int DefaultChatTimeoutSeconds = 60;

    /// <summary>How many top-ranked chunks go into the prompt.</summary>
    [Range(1, 20)]
    public int TopK { get; init; } = DefaultTopK;

    /// <summary>Total chunk characters in the prompt; the lowest-ranked chunks are dropped to fit.</summary>
    [Range(1_000, 50_000)]
    public int MaxContextChars { get; init; } = DefaultMaxContextChars;

    /// <summary>Timeout of each chat HTTP call.</summary>
    [Range(5, 300)]
    public int ChatTimeoutSeconds { get; init; } = DefaultChatTimeoutSeconds;
}
