namespace ContractReview.Domain.Claims;

/// <summary>A contract clause the evaluation relied on: the section heading and an excerpt of the stored chunk text.</summary>
public sealed record ClaimCitation(string SectionHeading, string Excerpt);
