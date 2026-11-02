using ContractReview.Application.Features.Claims.Retrieval;
using ContractReview.Domain.Contracts;

namespace ContractReview.UnitTests.Application;

public class ClaimRetrieverTests
{
    private readonly ClaimRetriever retriever = new();

    private static ChunkCandidate Chunk(int section, string heading, string text, params float[] vector) =>
        new(section, heading, 0, text, EmbeddingVector.ToBytes(vector));

    [Fact]
    public void Ranks_by_cosine_similarity_and_labels_from_one()
    {
        var chunks = new[]
        {
            Chunk(0, "FAR", "far", 0, 1),
            Chunk(1, "NEAR", "near", 1, 0.1f),
            Chunk(2, "MID", "mid", 1, 1),
        };

        var result = retriever.Retrieve([1, 0], chunks, 3, 1000);

        Assert.Equal(["NEAR", "MID", "FAR"], result.Select(r => r.SectionHeading));
        Assert.Equal(["C1", "C2", "C3"], result.Select(r => r.Label));
    }

    [Fact]
    public void Ignores_vector_length_so_scaled_vectors_rank_equal_by_direction()
    {
        var chunks = new[] { Chunk(0, "A", "a", 10, 0), Chunk(1, "B", "b", 0, 1) };

        var result = retriever.Retrieve([1, 0], chunks, 1, 1000);

        Assert.Equal("A", Assert.Single(result).SectionHeading);
        Assert.Equal(1, result[0].Score, 6);
    }

    [Fact]
    public void Takes_only_top_k()
    {
        var chunks = Enumerable.Range(0, 5).Select(i => Chunk(i, $"S{i}", "t", 1, i)).ToList();

        Assert.Equal(2, retriever.Retrieve([0, 1], chunks, 2, 1000).Count);
    }

    [Fact]
    public void Drops_lowest_ranked_chunks_to_fit_the_context_limit()
    {
        var chunks = new[]
        {
            Chunk(0, "BEST", new string('a', 600), 1, 0),
            Chunk(1, "SECOND", new string('b', 600), 1, 1),
        };

        var result = retriever.Retrieve([1, 0], chunks, 2, 1000);

        Assert.Equal("BEST", Assert.Single(result).SectionHeading);
    }

    [Fact]
    public void Always_keeps_the_best_chunk_even_when_it_alone_exceeds_the_limit()
    {
        var chunks = new[] { Chunk(0, "BIG", new string('a', 5000), 1, 0) };

        Assert.Single(retriever.Retrieve([1, 0], chunks, 1, 1000));
    }

    [Fact]
    public void Skips_chunks_with_a_different_dimension_and_zero_vectors()
    {
        var chunks = new[] { Chunk(0, "SHORT", "s", 1), Chunk(1, "ZERO", "z", 0, 0), Chunk(2, "OK", "o", 1, 1) };

        Assert.Equal("OK", Assert.Single(retriever.Retrieve([1, 1], chunks, 5, 1000)).SectionHeading);
    }

    [Fact]
    public void Returns_nothing_for_a_zero_query()
    {
        Assert.Empty(retriever.Retrieve([0, 0], [Chunk(0, "A", "a", 1, 1)], 5, 1000));
    }
}
