using ContractReview.Application.Abstractions.Pdf;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Infrastructure.Pdf;
using ContractReview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractReview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString(ConfigKeys.AppDbConnection)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
        return services;
    }
}
