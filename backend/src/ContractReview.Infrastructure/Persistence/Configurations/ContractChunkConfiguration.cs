using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractReview.Infrastructure.Persistence.Configurations;

public sealed class ContractChunkConfiguration : IEntityTypeConfiguration<ContractChunk>
{
    public const string TableName = "ContractChunks";

    public void Configure(EntityTypeBuilder<ContractChunk> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(ch => ch.Id);
        builder.Property(ch => ch.Id).ValueGeneratedNever();
        builder.Property(ch => ch.SectionOrder).IsRequired();
        builder.Property(ch => ch.SectionHeading).HasMaxLength(ContractRules.MaxSectionHeadingLength).IsRequired();
        builder.Property(ch => ch.ChunkOrder).IsRequired();
        builder.Property(ch => ch.Text).IsRequired();

        // float32 little-endian; LocalDB (SQL Server 2019) has no native vector type.
        builder.Property(ch => ch.Embedding).IsRequired();
        builder.HasIndex(ch => new { ch.ContractId, ch.SectionOrder, ch.ChunkOrder }).IsUnique();
    }
}
