using ContractReview.Domain.Claims;
using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContractReview.Infrastructure.Persistence.Configurations;

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public const string TableName = "Claims";

    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.ClaimText).HasMaxLength(ClaimRules.MaxTextLength).IsRequired();
        builder.Property(c => c.CoverageLevelRequested).HasMaxLength(ClaimRules.MaxCoverageLevelLength);
        builder.Property(c => c.State)
            .HasConversion<string>()
            .HasMaxLength(ClaimRules.MaxStateLength)
            .IsRequired();
        builder.Property(c => c.ErrorCode).HasMaxLength(ClaimRules.MaxErrorCodeLength);
        builder.Property(c => c.CreatedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(c => c.CompletedAtUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(ClaimRules.MaxStatusLength);
        builder.Property(c => c.Confidence);
        builder.Property(c => c.CoverageLevel).HasMaxLength(ClaimRules.MaxCoverageLevelLength);
        builder.Property(c => c.NeedsCoverageLevel).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.ResultJson).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Model).HasMaxLength(ClaimRules.MaxModelLength);
        builder.Ignore(c => c.Result);
        builder.Ignore(c => c.IsEvaluating);
        builder.HasIndex(c => new { c.ContractId, c.CreatedAtUtc });

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(c => c.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
