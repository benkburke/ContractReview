namespace ContractReview.Application.Abstractions.Chat;

/// <summary>The chat service failed, timed out, is not configured, or returned output that does not match the schema.</summary>
public sealed class ChatFailedException(string reason, Exception? innerException = null)
    : Exception(reason, innerException)
{
    public string Reason { get; } = reason;
}
