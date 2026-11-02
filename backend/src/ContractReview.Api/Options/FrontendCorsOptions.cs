using System.ComponentModel.DataAnnotations;

namespace ContractReview.Api.Options;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string DefaultFrontendOrigin = "http://localhost:3000";

    [Required]
    public string FrontendOrigin { get; init; } = DefaultFrontendOrigin;
}
