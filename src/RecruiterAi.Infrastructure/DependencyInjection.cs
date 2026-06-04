using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruiterAi.Infrastructure.Persistence;

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

        return services;
    }
}
