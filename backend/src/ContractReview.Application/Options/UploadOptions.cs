using System.ComponentModel.DataAnnotations;

namespace ContractReview.Application.Options;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";
    public const long DefaultMaxFileBytes = 10L * 1024 * 1024;

    /// <summary>Extra bytes allowed on top of the file for the multipart envelope (title field, boundaries).</summary>
    public const long RequestOverheadBytes = 1024 * 1024;

    [Range(1, long.MaxValue - RequestOverheadBytes)]
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    public long MaxRequestBytes => MaxFileBytes + RequestOverheadBytes;
}
