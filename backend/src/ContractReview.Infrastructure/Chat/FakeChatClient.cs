using System.Text.RegularExpressions;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Domain.Claims;

namespace ContractReview.Infrastructure.Chat;

/// <summary>
/// Deterministic, local stand-in for the chat model in tests, CI and e2e (D-028). It never calls the network and
/// derives its answer only from the claim text and the retrieved chunks, in this order:
/// <list type="number">
/// <item>
/// <b>Coverage level.</b> A chunk sentence "Coverage Level: X" gives the level X; if X is blank (empty, only
/// underscores, dashes or brackets, or the word blank, none, tbd or n/a) the contract leaves the level blank. A
/// sentence "Coverage Levels: A, B and C" lists the tier names. A sentence ends at a line break, period or semicolon.
/// A level supplied by the user always wins.
/// </item>
/// <item>
/// <b>needsCoverageLevel.</b> Blank level and none supplied: InsufficientInformation, needsCoverageLevel true, the
/// listed tier names as options (or <see cref="DefaultTiers"/> when none are listed), confidence 15.
/// </item>
/// <item>
/// <b>Vague claim.</b> The claim contains a vague word (weird, strange, odd, funny, noise, noisy, sounds, something,
/// stuff, thing), or has no part keyword (words of 3+ letters that are not stop words or generic words such as car or
/// broken), or no part keyword appears in any retrieved chunk: InsufficientInformation with a follow-up question, no
/// citations, confidence 20.
/// </item>
/// <item>
/// <b>Matched chunks.</b> Chunks containing at least one part keyword (case-insensitive, a trailing "s" ignored).
/// If any matched chunk has a heading containing "exclu" or "not covered": NotCovered citing those chunks (the
/// exclusions win), excluded items are the matched keywords, confidence 85.
/// Otherwise, if a matched chunk contains a condition marker (if, provided that, only when, only if, subject to,
/// unless, waiting period): Conditional citing the chunks with a marker, covered items are the keywords, confidence 70.
/// Otherwise Covered citing all matched chunks (at most 10), covered items are the keywords, confidence 85.
/// </item>
/// </list>
/// The coverage level in the result is the supplied or inferred level, else null. Citations are chunk labels.
/// </summary>
public sealed partial class FakeChatClient : IChatCompletionClient
{
    public const string Model = "fake/rule-based-claim-evaluator";

    public const string FollowUpQuestionText =
        "Please describe the problem in more detail: which part or system is affected, what symptoms you notice and when they started.";

    public const string ReasoningNeedsLevel = "The contract leaves the coverage level blank, so a coverage level is needed to decide. State amendments may apply.";
    public const string ReasoningVague = "The claim does not name a specific part or problem that the contract text can be checked against. State amendments may apply.";
    public const string ReasoningNotCovered = "An exclusion in the contract applies to the claimed item. State amendments may apply.";
    public const string ReasoningConditional = "The contract covers the claimed item only under stated conditions. State amendments may apply.";
    public const string ReasoningCovered = "The contract lists the claimed item as covered. State amendments may apply.";

    public const int ConfidenceNeedsLevel = 15;
    public const int ConfidenceVague = 20;
    public const int ConfidenceConditional = 70;
    public const int ConfidenceDecisive = 85;

    public static readonly IReadOnlyList<string> DefaultTiers = ["Powertrain", "Gold", "Platinum"];

    private static readonly string[] VagueWords =
        ["weird", "strange", "odd", "funny", "noise", "noisy", "sounds", "something", "stuff", "thing"];

    private static readonly string[] ExclusionHeadingMarkers = ["exclu", "not covered"];

    private static readonly string[] ConditionMarkers =
        ["if ", "provided that", "only when", "only if", "subject to", "unless", "waiting period"];

    private static readonly string[] BlankLevelWords = ["blank", "none", "tbd", "n/a"];

    private static readonly HashSet<string> IgnoredWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "are", "was", "has", "have", "had", "with", "not", "can", "you", "this", "that", "what",
        "does", "did", "how", "why", "when", "who", "from", "its", "into", "but", "any", "all", "our", "out", "get",
        "got", "been", "will", "would", "could", "should", "may", "might", "covered", "cover", "covers", "coverage",
        "claim", "please", "there", "they", "them", "than", "then", "also", "just", "very",
        "car", "vehicle", "auto", "broken", "break", "breaks", "problem", "issue", "wrong", "working", "work", "works",
        "stopped", "failed", "fail", "fails", "need", "needs", "fix", "repair", "bad", "dead",
    };

    public string ModelName => Model;

    public Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(prompt));
    }

    public static LlmClaimAnswer Evaluate(ClaimPrompt prompt)
    {
        var allText = string.Join('\n', prompt.Chunks.Select(c => c.Text));
        var contractLevel = ReadLevel(allText);
        var level = prompt.CoverageLevel ?? (contractLevel is { IsBlank: false } known ? known.Value : null);

        if (prompt.CoverageLevel is null && contractLevel is { IsBlank: true })
        {
            var tiers = ReadTiers(allText);
            return Answer(
                ClaimStatus.InsufficientInformation,
                ConfidenceNeedsLevel,
                null,
                true,
                tiers.Count > 0 ? tiers : DefaultTiers,
                [],
                [],
                ReasoningNeedsLevel,
                FollowUpQuestionText,
                []);
        }

        var keywords = Keywords(prompt.ClaimText);
        var matched = prompt.Chunks
            .Select(chunk => (Chunk: chunk, Hits: keywords.Where(k => ContainsWord(chunk.Text, k)).ToList()))
            .Where(x => x.Hits.Count > 0)
            .ToList();
        var isVague = VagueWords.Any(w => ContainsWord(prompt.ClaimText, w)) || keywords.Count == 0 || matched.Count == 0;
        if (isVague)
        {
            return Answer(
                ClaimStatus.InsufficientInformation,
                ConfidenceVague,
                level,
                false,
                [],
                [],
                [],
                ReasoningVague,
                FollowUpQuestionText,
                []);
        }

        var hitWords = matched.SelectMany(x => x.Hits).Distinct().ToList();
        var exclusions = matched
            .Where(x => ExclusionHeadingMarkers.Any(m => x.Chunk.SectionHeading.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (exclusions.Count > 0)
        {
            return Answer(
                ClaimStatus.NotCovered, ConfidenceDecisive, level, false, [], [], hitWords, ReasoningNotCovered, null, Labels(exclusions));
        }

        var conditional = matched
            .Where(x => ConditionMarkers.Any(m => x.Chunk.Text.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (conditional.Count > 0)
        {
            return Answer(
                ClaimStatus.Conditional, ConfidenceConditional, level, false, [], hitWords, [], ReasoningConditional, null, Labels(conditional));
        }

        return Answer(ClaimStatus.Covered, ConfidenceDecisive, level, false, [], hitWords, [], ReasoningCovered, null, Labels(matched));
    }

    private static LlmClaimAnswer Answer(
        ClaimStatus status,
        int confidence,
        string? level,
        bool needsLevel,
        IReadOnlyList<string> options,
        IReadOnlyList<string> covered,
        IReadOnlyList<string> excluded,
        string reasoning,
        string? followUp,
        IReadOnlyList<string> labels) =>
        new(status, confidence, level, needsLevel, options, covered, excluded, reasoning, followUp, labels);

    private static List<string> Labels(List<(PromptChunk Chunk, List<string> Hits)> chunks) =>
        chunks.Select(x => x.Chunk.Label).Take(ClaimRules.MaxCitations).ToList();

    private static List<string> Keywords(string text) =>
        WordPattern().Matches(text.ToLowerInvariant())
            .Select(m => Stem(m.Value))
            .Where(w => w.Length >= 3 && !IgnoredWords.Contains(w))
            .Distinct()
            .Take(ClaimRules.MaxCoveredItems)
            .ToList();

    private static string Stem(string word) => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word;

    private static bool ContainsWord(string text, string stem) =>
        WordPattern().Matches(text.ToLowerInvariant()).Any(m => Stem(m.Value) == stem);

    private static (string Value, bool IsBlank)? ReadLevel(string text)
    {
        var match = LevelPattern().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["v"].Value.Trim();
        var stripped = value.Trim('_', '-', '[', ']', '(', ')', ' ');
        var blank = stripped.Length == 0 || BlankLevelWords.Contains(stripped, StringComparer.OrdinalIgnoreCase);
        return (value.Length > ClaimRules.MaxCoverageLevelLength ? value[..ClaimRules.MaxCoverageLevelLength] : value, blank);
    }

    private static List<string> ReadTiers(string text)
    {
        var match = TiersPattern().Match(text);
        if (!match.Success)
        {
            return [];
        }

        return TierSeparator().Split(match.Groups["v"].Value)
            .Select(t => t.Trim())
            .Where(t => t.Length is > 0 and <= ClaimRules.MaxCoverageLevelLength)
            .Take(ClaimRules.MaxCoverageLevelOptions)
            .ToList();
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"coverage level\s*:\s*(?<v>[^\r\n.;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex LevelPattern();

    [GeneratedRegex(@"coverage levels\s*:\s*(?<v>[^\r\n.;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex TiersPattern();

    [GeneratedRegex(@"\s*(?:,|\band\b|\bor\b)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex TierSeparator();
}
