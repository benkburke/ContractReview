namespace ContractReview.Application.Abstractions.Chat;

/// <summary>Internal failure reasons for logs; clients only ever see <c>claim.evaluation_failed</c>.</summary>
public static class ChatFailureReasons
{
    public const string MissingApiKey = "The chat API key is not configured.";
    public const string HttpError = "The chat service returned an error status.";
    public const string Timeout = "The chat service timed out.";
    public const string TransportError = "The chat service could not be reached.";
    public const string MalformedOutput = "The chat service returned output that does not match the schema.";
}
