using ContractReview.Application.Features.Contracts.Indexing;
using ContractReview.Application.Options;
using ContractReview.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace ContractReview.UnitTests.Application;

public class ContractChunkerTests
{
    private static ContractChunker Chunker(int size = 200, int overlap = 50) =>
        new(Options.Create(new IndexingOptions { ChunkSize = size, ChunkOverlap = overlap }));

    private static string Letters(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(i => (char)('a' + (i % 26))));

    [Theory]
    [InlineData("SCHEDULE OF COVERAGE")]
    [InlineData("WHAT IS NOT COVERED")]
    [InlineData("SECTION 2: EXCLUSIONS (A-B)")]
    [InlineData("TERMS & CONDITIONS, PART 1/2.")]
    [InlineData("ABC")]
    public void IsHeading_accepts_upper_case_lines(string line) =>
        Assert.True(ContractChunker.IsHeading(line));

    [Theory]
    [InlineData("Schedule of Coverage")]
    [InlineData("SCHEDULE OF COVERAGe")]
    [InlineData("AB")]
    [InlineData("A1 B2")]
    [InlineData("12345 678")]
    [InlineData("WHAT IS COVERED?")]
    [InlineData("NOTICE!")]
    [InlineData("")]
    public void IsHeading_rejects_other_lines(string line) =>
        Assert.False(ContractChunker.IsHeading(line));

    [Fact]
    public void IsHeading_rejects_lines_over_100_characters()
    {
        Assert.True(ContractChunker.IsHeading(new string('A', 100)));
        Assert.False(ContractChunker.IsHeading(new string('A', 101)));
    }

    [Fact]
    public void Text_before_the_first_heading_goes_in_PREAMBLE()
    {
        var chunks = Chunker().Split("This warranty is issued by Acme.\n\nSCHEDULE OF COVERAGE\nEngine parts are covered.");

        Assert.Equal(2, chunks.Count);
        Assert.Equal(new ContractChunkDraft(0, ContractRules.PreambleHeading, 0, "This warranty is issued by Acme."), chunks[0]);
        Assert.Equal(new ContractChunkDraft(1, "SCHEDULE OF COVERAGE", 0, "Engine parts are covered."), chunks[1]);
    }

    [Fact]
    public void No_PREAMBLE_when_the_text_starts_with_a_heading()
    {
        var chunks = Chunker().Split("  WHAT IS COVERED  \r\nEngine parts.\r\n\r\nWHAT IS NOT COVERED\r\nTyres.");

        Assert.Equal(["WHAT IS COVERED", "WHAT IS NOT COVERED"], chunks.Select(c => c.SectionHeading));
        Assert.Equal([0, 1], chunks.Select(c => c.SectionOrder));
        Assert.Equal(["Engine parts.", "Tyres."], chunks.Select(c => c.Text));
    }

    [Fact]
    public void Consecutive_heading_lines_merge_into_one_heading()
    {
        var chunks = Chunker().Split("LIMITED WARRANTY\nSCHEDULE OF COVERAGE\nEngine parts are covered.");

        var chunk = Assert.Single(chunks);
        Assert.Equal("LIMITED WARRANTY SCHEDULE OF COVERAGE", chunk.SectionHeading);
    }

    [Fact]
    public void Sections_without_text_are_dropped_and_section_orders_stay_contiguous()
    {
        var chunks = Chunker().Split("PART ONE\n\nWHAT IS COVERED\nEngine.\n\nPART TWO\n\nWHAT IS NOT COVERED\nTyres.");

        Assert.Equal(["WHAT IS COVERED", "WHAT IS NOT COVERED"], chunks.Select(c => c.SectionHeading));
        Assert.Equal([0, 1], chunks.Select(c => c.SectionOrder));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n\t  \n")]
    [InlineData("SCHEDULE OF COVERAGE\n\n")]
    public void Empty_or_whitespace_text_gives_no_chunks(string text) =>
        Assert.Empty(Chunker().Split(text));

    [Fact]
    public void Paragraphs_are_packed_whole_into_chunks()
    {
        var p1 = new string('a', 80);
        var p2 = new string('b', 80);
        var p3 = new string('c', 80);

        var chunks = Chunker(size: 200, overlap: 0).Split($"{p1}\n\n{p2}\n\n{p3}");

        Assert.Equal([$"{p1}\n\n{p2}", p3], chunks.Select(c => c.Text));
        Assert.Equal([0, 1], chunks.Select(c => c.ChunkOrder));
    }

    [Fact]
    public void Long_paragraph_without_sentence_ends_is_hard_split_with_overlap()
    {
        var text = Letters(1000);

        var chunks = Chunker(size: 200, overlap: 50).Split(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 200));
        Assert.Equal(200, chunks[0].Text.Length);
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.Equal(chunks[i - 1].Text[^50..], chunks[i].Text[..50]);
            Assert.Equal(i, chunks[i].ChunkOrder);
        }

        // Dropping each chunk's overlap rebuilds the original text exactly.
        var rebuilt = chunks[0].Text + string.Concat(chunks.Skip(1).Select(c => c.Text[50..]));
        Assert.Equal(text, rebuilt);
    }

    [Fact]
    public void Long_paragraph_is_split_at_sentence_ends()
    {
        var sentences = string.Join(" ", Enumerable.Range(10, 30).Select(i => $"Clause {i} covers part {i}."));

        var chunks = Chunker(size: 200, overlap: 50).Split(sentences);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 200));
        Assert.All(chunks.SkipLast(1), c => Assert.EndsWith(".", c.Text));
    }

    [Fact]
    public void Chunks_respect_the_default_size_and_overlap()
    {
        var chunker = new ContractChunker(Options.Create(new IndexingOptions()));
        var text = "WHAT IS COVERED\n" + Letters(5000);

        var chunks = chunker.Split(text);

        Assert.All(chunks, c => Assert.True(c.Text.Length <= IndexingOptions.DefaultChunkSize));
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.Equal(
                chunks[i - 1].Text[^IndexingOptions.DefaultChunkOverlap..],
                chunks[i].Text[..IndexingOptions.DefaultChunkOverlap]);
        }
    }

    [Fact]
    public void Chunks_never_cross_sections()
    {
        var covered = new string('x', 450);
        var excluded = new string('y', 450);

        var chunks = Chunker(size: 200, overlap: 50).Split($"WHAT IS COVERED\n{covered}\nWHAT IS NOT COVERED\n{excluded}");

        var first = chunks.Where(c => c.SectionOrder == 0).ToList();
        var second = chunks.Where(c => c.SectionOrder == 1).ToList();
        Assert.Equal(chunks.Count, first.Count + second.Count);
        Assert.All(first, c => Assert.Equal("WHAT IS COVERED", c.SectionHeading));
        Assert.All(second, c => Assert.Equal("WHAT IS NOT COVERED", c.SectionHeading));
        Assert.All(first, c => Assert.DoesNotContain("y", c.Text));
        Assert.All(second, c => Assert.DoesNotContain("x", c.Text));
        Assert.Equal(Enumerable.Range(0, first.Count), first.Select(c => c.ChunkOrder));
        Assert.Equal(Enumerable.Range(0, second.Count), second.Select(c => c.ChunkOrder));
    }

    [Fact]
    public void Merged_headings_are_cut_to_the_stored_heading_length()
    {
        var line = new string('A', 90);
        var text = string.Join("\n", Enumerable.Repeat(line, 3)) + "\nBody text.";

        var chunk = Assert.Single(Chunker().Split(text));

        Assert.Equal(ContractRules.MaxSectionHeadingLength, chunk.SectionHeading.Length);
    }
}
