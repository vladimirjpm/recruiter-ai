using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Options;
using RecruiterAi.Infrastructure.Pdf;
using RecruiterAi.Infrastructure.Persistence;
using RecruiterAi.Infrastructure.Services;

namespace RecruiterAi.Infrastructure;

/// <summary>
/// Registers infrastructure services in the DI container.
///
/// Called from Program.cs:
///   builder.Services.AddInfrastructure(builder.Configuration);
///
/// Phase 1: DbContext only.
/// Phase 2: also registers IEmbeddingService and a pgvector-backed
/// ICandidateSearchService implementation.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not set (check appsettings.json / environment variables).");

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connectionString, npgsql =>
            {
                // Migrations live in the Infrastructure assembly, not the API project.
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
            }));

        services.AddScoped<ICvParserService, PdfPigCvParser>();

        // OpenAI options bound from "OpenAI" section; key validation is deferred to
        // service construction so tests can replace the implementation with a stub.
        // TODO Future: add an IHostedService startup check that validates Llm:ApiKey is set
        // and optionally pings the OpenAI API — surfaces misconfiguration at boot, not on
        // the first user request.
        services.Configure<LlmOptions>(opt =>
        {
            opt.Provider       = configuration["Llm:Provider"]       ?? "OpenAI";
            opt.ApiKey         = configuration["Llm:ApiKey"]         ?? string.Empty;
            opt.Model          = configuration["Llm:Model"]          ?? "gpt-4o-mini";
            opt.EmbeddingModel = configuration["Llm:EmbeddingModel"] ?? "text-embedding-3-small";
        });
        services.AddScoped<IResumeEvaluationService, OpenAiResumeEvaluationService>();
        services.AddScoped<ICvGenerationService, OpenAiCvGenerationService>();

        return services;
    }
}
