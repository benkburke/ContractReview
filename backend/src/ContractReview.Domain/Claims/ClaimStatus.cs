using System.Text.Json.Serialization;

namespace ContractReview.Domain.Claims;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimStatus
{
    Covered,
    NotCovered,
    Conditional,
    InsufficientInformation,
}
