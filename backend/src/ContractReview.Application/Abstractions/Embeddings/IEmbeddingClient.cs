namespace ContractReview.Application.Abstractions.Embeddings;

/// <summary>Turns texts into embedding vectors. One call is one request; callers batch.</summary>
public interface IEmbeddingClient
{
    /// <summary>The model name stored with the contract.</summary>
    string ModelName { get; }

    /// <summary>Returns one vector per input, in input order. Throws <see cref="EmbeddingFailedException"/> on any failure.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}
