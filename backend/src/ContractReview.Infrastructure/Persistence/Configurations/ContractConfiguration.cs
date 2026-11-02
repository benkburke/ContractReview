using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractReview.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public const string TableName = "Contracts";

    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(ContractRules.MaxTitleLength).IsRequired();
        builder.Property(c => c.FileName).HasMaxLength(ContractRules.MaxFileNameLength).IsRequired();
        builder.Property(c => c.ContentType).HasMaxLength(ContractRules.MaxContentTypeLength).IsRequired();
        builder.Property(c => c.SizeBytes).IsRequired();
        builder.Property(c => c.PageCount).IsRequired();
        builder.Property(c => c.FileContent).IsRequired();
        builder.Property(c => c.ExtractedText).IsRequired();
        builder.Property(c => c.UploadedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();
        builder.HasIndex(c => c.UploadedAtUtc);

        // IndexStatus is a concurrency token so two callers cannot both claim the same contract for indexing.
        builder.Property(c => c.IndexStatus)
            .HasConversion<string>()
            .HasMaxLength(ContractRules.MaxIndexStatusLength)
            .HasDefaultValue(IndexStatus.NotIndexed)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(c => c.IndexError).HasMaxLength(ContractRules.MaxIndexErrorLength);
        builder.Property(c => c.IndexedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        builder.Property(c => c.EmbeddingModel).HasMaxLength(ContractRules.MaxEmbeddingModelLength);
        builder.Property(c => c.EmbeddingDimensions);
        builder.HasIndex(c => c.IndexStatus);

        builder.HasMany(c => c.Chunks)
            .WithOne()
            .HasForeignKey(ch => ch.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
