using ContractReview.Domain.Contracts;

namespace ContractReview.Application.Features.Claims.Retrieval;

public sealed class ClaimRetriever : IClaimRetriever
{
    public const string LabelPrefix = "C";

    public IReadOnlyList<RetrievedChunk> Retrieve(
        float[] query, IReadOnlyList<ChunkCandidate> chunks, int topK, int maxContextChars)
    {
        var queryNorm = Norm(query);
        if (queryNorm == 0)
        {
            return [];
        }

        var ranked = chunks
            .Select(chunk => (Chunk: chunk, Score: Cosine(query, queryNorm, EmbeddingVector.FromBytes(chunk.Embedding))))
            .Where(x => x.Score is not null)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.SectionOrder)
            .ThenBy(x => x.Chunk.ChunkOrder)
            .Take(topK)
            .ToList();

        var result = new List<RetrievedChunk>();
        var totalChars = 0;
        foreach (var (chunk, score) in ranked)
        {
            if (result.Count > 0 && totalChars + chunk.Text.Length > maxContextChars)
            {
                break;
            }

            totalChars += chunk.Text.Length;
            result.Add(new RetrievedChunk(
                LabelPrefix + (result.Count + 1), chunk.SectionHeading, chunk.Text, score!.Value));
        }

        return result;
    }

    private static double? Cosine(float[] query, double queryNorm, float[] vector)
    {
        if (vector.Length != query.Length)
        {
            return null;
        }

        double dot = 0;
        double sumSquares = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            dot += query[i] * vector[i];
            sumSquares += (double)vector[i] * vector[i];
        }

        var norm = Math.Sqrt(sumSquares);
        return norm == 0 ? null : dot / (queryNorm * norm);
    }

    private static double Norm(float[] vector)
    {
        double sumSquares = 0;
        foreach (var v in vector)
        {
            sumSquares += (double)v * v;
        }

        return Math.Sqrt(sumSquares);
    }
}
