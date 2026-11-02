using ContractReview.Domain.Contracts;

namespace ContractReview.Application.Features.Contracts;

public static class ContractSections
{
    /// <summary>Groups chunks into sections in document order with their chunk counts.</summary>
    public static IReadOnlyList<ContractSectionDto> FromChunks(IEnumerable<ContractChunk> chunks) =>
        chunks
            .GroupBy(c => (c.SectionOrder, c.SectionHeading))
            .OrderBy(g => g.Key.SectionOrder)
            .Select(g => new ContractSectionDto(g.Key.SectionOrder, g.Key.SectionHeading, g.Count()))
            .ToList();
}
