using ContractReview.Application.Abstractions.Embeddings;

namespace ContractReview.Infrastructure.Embeddings;

/// <summary>
/// Deterministic local embedder for tests, CI and e2e (D-028): a hashed bag of lower-cased words in a fixed number of
/// buckets, L2-normalized. Texts sharing words get a positive cosine similarity; no network calls.
/// </summary>
public sealed class FakeEmbeddingClient : IEmbeddingClient
{
    public const string Model = "fake/hashed-bag-of-words";
    public const int Dimensions = 256;

    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    public string ModelName => Model;

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<float[]> vectors = inputs.Select(Embed).ToList();
        return Task.FromResult(vectors);
    }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var hash = FnvOffsetBasis;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                hash = (hash ^ char.ToLowerInvariant(c)) * FnvPrime;
                inWord = true;
            }
            else if (inWord)
            {
                vector[hash % Dimensions]++;
                hash = FnvOffsetBasis;
                inWord = false;
            }
        }

        if (inWord)
        {
            vector[hash % Dimensions]++;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }
}
