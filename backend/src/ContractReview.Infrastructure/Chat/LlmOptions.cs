namespace ContractReview.Infrastructure.Chat;

public enum LlmProvider
{
    OpenRouter,
    Fake,
}

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary><see cref="LlmProvider.Fake"/> in tests, CI and e2e (D-028); never calls OpenRouter.</summary>
    public LlmProvider Provider { get; init; } = LlmProvider.OpenRouter;
}
