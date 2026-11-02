using System.Text.Json.Serialization;

namespace ContractReview.Domain.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IndexStatus
{
    NotIndexed,
    Indexing,
    Indexed,
    Failed,
}
