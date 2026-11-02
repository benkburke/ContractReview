using System.Text;
using ContractReview.Application.Abstractions.Chat;

namespace ContractReview.Infrastructure.Chat;

/// <summary>Lays out the claim and the labelled chunks as the user message.</summary>
public static class ClaimPromptBuilder
{
    public static string BuildUserMessage(ClaimPrompt prompt)
    {
        var message = new StringBuilder();
        message.AppendLine(ClaimSystemPrompt.ClaimHeading);
        message.AppendLine(prompt.ClaimText);
        message.AppendLine();
        message.Append(ClaimSystemPrompt.CoverageLevelHeading)
            .Append(' ')
            .AppendLine(prompt.CoverageLevel ?? ClaimSystemPrompt.NoCoverageLevel);
        message.AppendLine();
        message.AppendLine(ClaimSystemPrompt.ExcerptsHeading);
        foreach (var chunk in prompt.Chunks)
        {
            message.AppendLine();
            message.Append('[').Append(chunk.Label).Append("] ")
                .Append(ClaimSystemPrompt.SectionLabel).Append(' ').AppendLine(chunk.SectionHeading);
            message.AppendLine(chunk.Text);
        }

        return message.ToString();
    }
}
