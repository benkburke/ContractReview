using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<Claim> Claims => Set<Claim>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
