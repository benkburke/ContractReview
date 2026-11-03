using System.Text.Json.Serialization;

namespace ContractReview.Application.Features.Claims.Progress;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimProgressStep
{
    Retrieving,
    Reasoning,
    Completed,
    Failed,
}
