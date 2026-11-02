using System.Text.Json;
using System.Text.Json.Nodes;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Domain.Claims;

namespace ContractReview.Infrastructure.Chat;

/// <summary>Wire details of the OpenAI-compatible chat completions API served by OpenRouter.</summary>
public static class ChatApi
{
    public const string RoleSystem = "system";
    public const string RoleUser = "user";
    public const string ResponseFormatJsonSchema = "json_schema";
    public const string SchemaName = "claim_evaluation";
    public const double Temperature = 0;

    /// <summary>One more attempt after output that does not match the schema.</summary>
    public const int MalformedOutputRetries = 1;
}

internal sealed record ChatRequest(
    string Model,
    double Temperature,
    IReadOnlyList<ChatMessage> Messages,
    ChatResponseFormat ResponseFormat);

internal sealed record ChatMessage(string Role, string Content);

internal sealed record ChatResponseFormat(string Type, ChatJsonSchema JsonSchema);

internal sealed record ChatJsonSchema(string Name, bool Strict, JsonObject Schema);

internal sealed record ChatResponse(IReadOnlyList<ChatChoice>? Choices);

internal sealed record ChatChoice(ChatResponseMessage? Message);

internal sealed record ChatResponseMessage(string? Content);

/// <summary>The JSON object the model must return. Missing fields or wrong types make the output malformed.</summary>
internal sealed record ChatAnswerWire
{
    public required string Status { get; init; }

    public required int Confidence { get; init; }

    public required string? CoverageLevel { get; init; }

    public required bool NeedsCoverageLevel { get; init; }

    public required List<string> CoverageLevelOptions { get; init; }

    public required List<string> CoveredItems { get; init; }

    public required List<string> ExcludedItems { get; init; }

    public required string Reasoning { get; init; }

    public required string? FollowUpQuestion { get; init; }

    /// <summary>Chunk labels such as "C1".</summary>
    public required List<string> Citations { get; init; }
}

internal static class ChatWire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectNullableAnnotations = true,
    };

    private static readonly JsonSerializerOptions AnswerJson = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
    };

    private const string KeyType = "type";
    private const string KeyAdditionalProperties = "additionalProperties";
    private const string KeyRequired = "required";
    private const string KeyProperties = "properties";
    private const string KeyEnum = "enum";
    private const string KeyItems = "items";
    private const string TypeObject = "object";
    private const string TypeString = "string";
    private const string TypeInteger = "integer";
    private const string TypeBoolean = "boolean";
    private const string TypeArray = "array";
    private const string TypeNull = "null";

    public static ChatRequest BuildRequest(string model, ClaimPrompt prompt) => new(
        model,
        ChatApi.Temperature,
        [
            new ChatMessage(ChatApi.RoleSystem, ClaimSystemPrompt.Text),
            new ChatMessage(ChatApi.RoleUser, ClaimPromptBuilder.BuildUserMessage(prompt)),
        ],
        new ChatResponseFormat(ChatApi.ResponseFormatJsonSchema, new ChatJsonSchema(ChatApi.SchemaName, true, AnswerSchema())));

    /// <summary>Parses the model's content into an answer; null when it is not valid JSON of the schema or breaks a limit.</summary>
    public static LlmClaimAnswer? ParseAnswer(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        ChatAnswerWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ChatAnswerWire>(content, AnswerJson);
        }
        catch (JsonException)
        {
            return null;
        }

        if (wire is null || !Enum.GetNames<ClaimStatus>().Contains(wire.Status))
        {
            return null;
        }

        var answer = new LlmClaimAnswer(
            Enum.Parse<ClaimStatus>(wire.Status),
            wire.Confidence,
            wire.CoverageLevel,
            wire.NeedsCoverageLevel,
            wire.CoverageLevelOptions,
            wire.CoveredItems,
            wire.ExcludedItems,
            wire.Reasoning,
            wire.FollowUpQuestion,
            wire.Citations);
        return answer.FindViolation() is null ? answer : null;
    }

    /// <summary>The strict JSON schema of <see cref="ChatAnswerWire"/>; limits are enforced server-side after parsing.</summary>
    private static JsonObject AnswerSchema() => new()
    {
        [KeyType] = TypeObject,
        [KeyAdditionalProperties] = false,
        [KeyRequired] = new JsonArray(Names().Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
        [KeyProperties] = new JsonObject
        {
            [Name(nameof(ChatAnswerWire.Status))] = new JsonObject
            {
                [KeyType] = TypeString,
                [KeyEnum] = new JsonArray(Enum.GetNames<ClaimStatus>().Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
            },
            [Name(nameof(ChatAnswerWire.Confidence))] = new JsonObject { [KeyType] = TypeInteger },
            [Name(nameof(ChatAnswerWire.CoverageLevel))] = Nullable(TypeString),
            [Name(nameof(ChatAnswerWire.NeedsCoverageLevel))] = new JsonObject { [KeyType] = TypeBoolean },
            [Name(nameof(ChatAnswerWire.CoverageLevelOptions))] = StringArray(),
            [Name(nameof(ChatAnswerWire.CoveredItems))] = StringArray(),
            [Name(nameof(ChatAnswerWire.ExcludedItems))] = StringArray(),
            [Name(nameof(ChatAnswerWire.Reasoning))] = new JsonObject { [KeyType] = TypeString },
            [Name(nameof(ChatAnswerWire.FollowUpQuestion))] = Nullable(TypeString),
            [Name(nameof(ChatAnswerWire.Citations))] = StringArray(),
        },
    };

    private static IEnumerable<string> Names() =>
    [
        Name(nameof(ChatAnswerWire.Status)),
        Name(nameof(ChatAnswerWire.Confidence)),
        Name(nameof(ChatAnswerWire.CoverageLevel)),
        Name(nameof(ChatAnswerWire.NeedsCoverageLevel)),
        Name(nameof(ChatAnswerWire.CoverageLevelOptions)),
        Name(nameof(ChatAnswerWire.CoveredItems)),
        Name(nameof(ChatAnswerWire.ExcludedItems)),
        Name(nameof(ChatAnswerWire.Reasoning)),
        Name(nameof(ChatAnswerWire.FollowUpQuestion)),
        Name(nameof(ChatAnswerWire.Citations)),
    ];

    private static string Name(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);

    private static JsonObject Nullable(string type) => new()
    {
        [KeyType] = new JsonArray((JsonNode)JsonValue.Create(type)!, JsonValue.Create(TypeNull)!),
    };

    private static JsonObject StringArray() => new()
    {
        [KeyType] = TypeArray,
        [KeyItems] = new JsonObject { [KeyType] = TypeString },
    };
}
