namespace ContractReview.Application.Abstractions.Chat;

/// <summary>What the model sees: the claim, the optional user-supplied level and the retrieved chunks.</summary>
public sealed record ClaimPrompt(string ClaimText, string? CoverageLevel, IReadOnlyList<PromptChunk> Chunks);

/// <summary>A retrieved chunk the model can cite by its label (C1, C2, ...).</summary>
public sealed record PromptChunk(string Label, string SectionHeading, string Text);
