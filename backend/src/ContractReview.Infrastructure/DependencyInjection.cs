using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Application.Abstractions.Pdf;
using ContractReview.Application.Abstractions.Persistence;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Options;
using ContractReview.Infrastructure.Chat;
using ContractReview.Infrastructure.Embeddings;
using ContractReview.Infrastructure.Indexing;
using ContractReview.Infrastructure.Pdf;
using ContractReview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddEmbeddings();
        services.AddChat();
        services.AddHostedService<ContractIndexBackfillService>();
        return services;
    }

    private static void AddEmbeddings(this IServiceCollection services)
    {
        services.AddOptions<EmbeddingsOptions>()
            .BindConfiguration(EmbeddingsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OpenRouterOptions>()
            .BindConfiguration(OpenRouterOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<OpenRouterEmbeddingClient>((sp, client) =>
            OpenRouterEmbeddingClient.Configure(client, sp.GetRequiredService<IOptions<OpenRouterOptions>>().Value));
        services.AddSingleton<FakeEmbeddingClient>();

        // Chosen per resolve from options so test hosts can switch provider through configuration (D-028).
        services.AddScoped<IEmbeddingClient>(sp =>
            sp.GetRequiredService<IOptions<EmbeddingsOptions>>().Value.Provider == EmbeddingProvider.Fake
                ? sp.GetRequiredService<FakeEmbeddingClient>()
                : sp.GetRequiredService<OpenRouterEmbeddingClient>());
    }

    private static void AddChat(this IServiceCollection services)
    {
        services.AddOptions<LlmOptions>()
            .BindConfiguration(LlmOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<OpenRouterChatClient>((sp, client) =>
            OpenRouterChatClient.Configure(
                client,
                sp.GetRequiredService<IOptions<OpenRouterOptions>>().Value,
                sp.GetRequiredService<IOptions<ClaimsOptions>>().Value));
        services.AddSingleton<FakeChatClient>();

        // Chosen per resolve from options so test hosts can switch provider through configuration (D-028).
        services.AddScoped<IChatCompletionClient>(sp =>
            sp.GetRequiredService<IOptions<LlmOptions>>().Value.Provider == LlmProvider.Fake
                ? sp.GetRequiredService<FakeChatClient>()
                : sp.GetRequiredService<OpenRouterChatClient>());
    }
}
