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
    }
}
