using ContractReview.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContractReview.IntegrationTests;

/// <summary>One factory and one LocalDB database (migrated once, dropped on dispose) for the whole assembly.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database=ContractReview_Test_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";

    public ApiFactory()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }

    // Fake embedder so tests never call OpenRouter (D-028); no startup backfill racing the migration or the tests.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting("ConnectionStrings:AppDb", connectionString)
            .UseSetting("Embeddings:Provider", "Fake")
            .UseSetting("Indexing:BackfillOnStartup", "false");

    private bool databaseDropped;

    public override async ValueTask DisposeAsync()
    {
        DropDatabase();
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DropDatabase();
        }

        base.Dispose(disposing);
    }

    private void DropDatabase()
    {
        if (databaseDropped)
        {
            return;
        }

        databaseDropped = true;
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
