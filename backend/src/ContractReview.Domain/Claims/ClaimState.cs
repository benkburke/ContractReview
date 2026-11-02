using System.Text.Json.Serialization;

namespace ContractReview.Domain.Claims;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimState
{
    Evaluated,
    Failed,
}
