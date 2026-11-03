using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Features.Claims.Evaluation;
using ContractReview.Domain.Claims;

namespace ContractReview.Infrastructure.Chat;

/// <summary>The JSON object the reviewer model must return. Missing fields or wrong types make the output malformed.</summary>
internal sealed record ReviewAnswerWire
{
    public required bool AgreesWithPrimary { get; init; }

    public required bool IsCovered { get; init; }

    public required int Confidence { get; init; }

    public required string AccuracyExplanation { get; init; }

    public required List<string> CoveredItems { get; init; }

    public required List<string> NotCoveredItems { get; init; }
}

internal static class ReviewWire
{
    public const string SchemaName = "claim_review";

    public const string ReasonNotJsonOfSchema = "content is not valid JSON of the schema";

    private static readonly JsonSerializerOptions AnswerJson = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
    };

    public static ChatRequest BuildRequest(string model, ClaimPrompt prompt, ClaimEvaluationResult primary) => new(
        model,
        ChatApi.Temperature,
        [
            new ChatMessage(ChatApi.RoleSystem, ReviewSystemPrompt.Text),
            new ChatMessage(ChatApi.RoleUser, BuildUserMessage(prompt, primary)),
        ],
        new ChatResponseFormat(ChatApi.ResponseFormatJsonSchema, new ChatJsonSchema(SchemaName, true, Schema())));

    public static string BuildUserMessage(ClaimPrompt prompt, ClaimEvaluationResult primary)
    {
        var message = new StringBuilder(ClaimPromptBuilder.BuildUserMessage(prompt));
        message.AppendLine();
        message.AppendLine(ReviewSystemPrompt.PrimaryAnswerHeading);
        message.Append(ReviewSystemPrompt.StatusLabel).Append(' ').AppendLine(primary.Status.ToString());
        message.Append(ReviewSystemPrompt.ConfidenceLabel).Append(' ').AppendLine(primary.Confidence.ToString());
        message.Append(ReviewSystemPrompt.CoverageLevelLabel).Append(' ')
            .AppendLine(primary.CoverageLevel ?? ReviewSystemPrompt.None);
        message.Append(ReviewSystemPrompt.ReasoningLabel).Append(' ').AppendLine(primary.Reasoning);
        message.Append(ReviewSystemPrompt.CoveredItemsLabel).Append(' ').AppendLine(Join(primary.CoveredItems));
        message.Append(ReviewSystemPrompt.ExcludedItemsLabel).Append(' ').AppendLine(Join(primary.ExcludedItems));
        message.Append(ReviewSystemPrompt.CitedSectionsLabel).Append(' ')
            .AppendLine(Join(primary.Citations.Select(c => c.SectionHeading).ToList()));
        return message.ToString();
    }

    /// <summary>Parses the model's content into a review; null when it is not valid JSON of the schema or breaks a limit.</summary>
    public static LlmClaimReview? ParseReview(string? content, out string? failureReason)
    {
        failureReason = ReasonNotJsonOfSchema;
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        ReviewAnswerWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ReviewAnswerWire>(content, AnswerJson);
        }
        catch (JsonException)
        {
            return null;
        }

        if (wire is null)
        {
            return null;
        }

        var review = new LlmClaimReview(
            wire.AgreesWithPrimary,
            wire.IsCovered,
            wire.Confidence,
            wire.AccuracyExplanation,
            wire.CoveredItems,
            wire.NotCoveredItems);
        failureReason = review.FindViolation();
        return failureReason is null ? review : null;
    }

    private static string Join(IReadOnlyList<string> items) =>
        items.Count == 0 ? ReviewSystemPrompt.None : string.Join(ReviewSystemPrompt.ItemSeparator, items);

    private static JsonObject Schema()
    {
        string[] names =
        [
            ChatWire.Name(nameof(ReviewAnswerWire.AgreesWithPrimary)),
            ChatWire.Name(nameof(ReviewAnswerWire.IsCovered)),
            ChatWire.Name(nameof(ReviewAnswerWire.Confidence)),
            ChatWire.Name(nameof(ReviewAnswerWire.AccuracyExplanation)),
            ChatWire.Name(nameof(ReviewAnswerWire.CoveredItems)),
            ChatWire.Name(nameof(ReviewAnswerWire.NotCoveredItems)),
        ];
        return new JsonObject
        {
            [ChatWire.KeyType] = ChatWire.TypeObject,
            [ChatWire.KeyAdditionalProperties] = false,
            [ChatWire.KeyRequired] = new JsonArray(names.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
            [ChatWire.KeyProperties] = new JsonObject
            {
                [names[0]] = new JsonObject { [ChatWire.KeyType] = ChatWire.TypeBoolean },
                [names[1]] = new JsonObject { [ChatWire.KeyType] = ChatWire.TypeBoolean },
                [names[2]] = new JsonObject { [ChatWire.KeyType] = ChatWire.TypeInteger },
                [names[3]] = new JsonObject { [ChatWire.KeyType] = ChatWire.TypeString },
                [names[4]] = ChatWire.StringArray(),
                [names[5]] = ChatWire.StringArray(),
            },
        };
    }
}
