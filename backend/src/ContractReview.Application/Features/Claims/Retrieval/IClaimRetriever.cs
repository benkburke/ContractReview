namespace ContractReview.Application.Features.Claims.Retrieval;

/// <summary>Ranks contract chunks by cosine similarity to a query vector. Pure: no database, no network.</summary>
public interface IClaimRetriever
{
    /// <summary>
    /// Returns up to <paramref name="topK"/> chunks, best first. Chunks whose vector length differs from the query are
    /// skipped. Lowest-ranked chunks are dropped until the total text fits <paramref name="maxContextChars"/>; the
    /// best chunk is always kept.
    /// </summary>
    IReadOnlyList<RetrievedChunk> Retrieve(
        float[] query, IReadOnlyList<ChunkCandidate> chunks, int topK, int maxContextChars);
}

/// <summary>A stored chunk with its embedding bytes (float32, little-endian).</summary>
public sealed record ChunkCandidate(int SectionOrder, string SectionHeading, int ChunkOrder, string Text, byte[] Embedding);

/// <summary>A ranked chunk. <see cref="Label"/> is C1 for the best chunk, C2 for the next, and so on.</summary>
public sealed record RetrievedChunk(string Label, string SectionHeading, string Text, double Score);
