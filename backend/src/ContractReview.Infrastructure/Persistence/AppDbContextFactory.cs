using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContractReview.Infrastructure.Persistence;

/// <summary>Design-time factory for all EF tooling. Refuses non-LocalDB servers unless explicitly allowed.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string NonLocalMessage =
        "Refusing to use a non-LocalDB server from EF tooling. Set EF_ALLOW_NON_LOCAL=1 to allow it.";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConfigKeys.EfConnection);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = ConfigKeys.DevelopmentConnectionString;
        }

        var dataSource = new SqlConnectionStringBuilder(connectionString).DataSource;
        var isLocal = dataSource.StartsWith(ConfigKeys.LocalDbServerPrefix, StringComparison.OrdinalIgnoreCase);
        var allowed = Environment.GetEnvironmentVariable(ConfigKeys.EfAllowNonLocal) == ConfigKeys.EfAllowNonLocalEnabled;
        if (!isLocal && !allowed)
        {
            throw new InvalidOperationException(NonLocalMessage);
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        return new AppDbContext(options);
    }
}
