namespace ContractReview.Application.Features.Contracts.Indexing;

public static class ChunkingRules
{
    public const int MinHeadingLength = 3;
    public const int MaxHeadingLength = 100;
    public const int MinHeadingLetters = 3;

    /// <summary>Non-letter, non-digit characters a heading line may contain besides spaces.</summary>
    public const string HeadingPunctuation = "&,.:()-/";

    public const string SentenceEndings = ".!?";
    public const string LineBreak = "\n";
    public const string ParagraphSeparator = "\n\n";
    public const string HeadingJoiner = " ";

    /// <summary>Between the heading and the chunk text in the embedded input, so the section informs similarity.</summary>
    public const string EmbeddingHeadingSeparator = "\n";
}
