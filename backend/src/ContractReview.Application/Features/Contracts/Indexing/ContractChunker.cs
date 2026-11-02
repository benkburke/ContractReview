using ContractReview.Application.Options;
using ContractReview.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace ContractReview.Application.Features.Contracts.Indexing;

/// <summary>
/// Splits text into sections at upper-case heading lines, then packs each section's paragraphs into chunks of at
/// most <see cref="IndexingOptions.ChunkSize"/> characters with <see cref="IndexingOptions.ChunkOverlap"/> characters
/// of overlap. Long paragraphs split at sentence ends, else hard. Chunks never cross sections.
/// </summary>
public sealed class ContractChunker(IOptions<IndexingOptions> options) : IContractChunker
{
    public IReadOnlyList<ContractChunkDraft> Split(string text)
    {
        var size = options.Value.ChunkSize;
        var overlap = options.Value.ChunkOverlap;
        var result = new List<ContractChunkDraft>();
        var sectionOrder = 0;

        foreach (var (heading, lines) in ReadSections(text))
        {
            var sectionText = string.Join(ChunkingRules.ParagraphSeparator, ToParagraphs(lines));
            var chunkOrder = 0;
            foreach (var chunk in Pack(sectionText, size, overlap))
            {
                result.Add(new ContractChunkDraft(sectionOrder, heading, chunkOrder++, chunk));
            }

            if (chunkOrder > 0)
            {
                sectionOrder++;
            }
        }

        return result;
    }

    /// <summary>True for a trimmed line of 3 to 100 characters with at least 3 letters, all upper case.</summary>
    public static bool IsHeading(string line)
    {
        if (line.Length < ChunkingRules.MinHeadingLength || line.Length > ChunkingRules.MaxHeadingLength)
        {
            return false;
        }

        var letters = 0;
        foreach (var c in line)
        {
            if (char.IsLetter(c))
            {
                if (!char.IsUpper(c))
                {
                    return false;
                }

                letters++;
            }
            else if (!char.IsDigit(c) && !char.IsWhiteSpace(c) && !ChunkingRules.HeadingPunctuation.Contains(c))
            {
                return false;
            }
        }

        return letters >= ChunkingRules.MinHeadingLetters;
    }

    private static List<(string Heading, List<string> Lines)> ReadSections(string text)
    {
        var sections = new List<(string Heading, List<string> Lines)>();
        var heading = ContractRules.PreambleHeading;
        var lines = new List<string>();
        var headingParts = new List<string>();
        var previousWasHeading = false;

        foreach (var rawLine in text.ReplaceLineEndings(ChunkingRules.LineBreak).Split(ChunkingRules.LineBreak))
        {
            var line = rawLine.Trim();
            if (IsHeading(line))
            {
                if (!previousWasHeading)
                {
                    sections.Add((heading, lines));
                    lines = [];
                    headingParts = [];
                }

                headingParts.Add(line);
                heading = TruncateHeading(string.Join(ChunkingRules.HeadingJoiner, headingParts));
                previousWasHeading = true;
                continue;
            }

            lines.Add(line);
            previousWasHeading = false;
        }

        sections.Add((heading, lines));
        return sections;
    }

    private static string TruncateHeading(string heading) =>
        heading.Length <= ContractRules.MaxSectionHeadingLength
            ? heading
            : heading[..ContractRules.MaxSectionHeadingLength].TrimEnd();

    private static List<string> ToParagraphs(List<string> lines)
    {
        var paragraphs = new List<string>();
        var current = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            current.Add(line);
        }

        Flush();
        return paragraphs;

        void Flush()
        {
            if (current.Count > 0)
            {
                paragraphs.Add(string.Join(ChunkingRules.LineBreak, current));
                current.Clear();
            }
        }
    }

    private static List<string> Pack(string text, int size, int overlap)
    {
        var chunks = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            int end;
            if (text.Length - start <= size)
            {
                end = text.Length;
            }
            else
            {
                // The end must lie past the overlap so every chunk adds new text.
                var min = start + overlap + 1;
                var max = start + size;
                end = LastParagraphBreak(text, min, max) ?? LastSentenceEnd(text, min, max) ?? max;
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (end >= text.Length)
            {
                break;
            }

            start = end - overlap;
        }

        return chunks;
    }

    private static int? LastParagraphBreak(string text, int min, int max)
    {
        for (var end = max; end >= min; end--)
        {
            if (string.CompareOrdinal(text, end, ChunkingRules.ParagraphSeparator, 0, ChunkingRules.ParagraphSeparator.Length) == 0)
            {
                return end;
            }
        }

        return null;
    }

    private static int? LastSentenceEnd(string text, int min, int max)
    {
        for (var end = max; end >= min; end--)
        {
            if (ChunkingRules.SentenceEndings.Contains(text[end - 1]) && (end == text.Length || char.IsWhiteSpace(text[end])))
            {
                return end;
            }
        }

        return null;
    }
}
