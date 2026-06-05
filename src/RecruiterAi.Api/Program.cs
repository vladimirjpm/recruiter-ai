using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RecruiterAi.Infrastructure;
using RecruiterAi.Infrastructure.Persistence;

// ASP.NET Core 10 Web API entry point.
var builder = WebApplication.CreateBuilder(args);

// Structured logging via the built-in ILogger (Serilog can be added later if needed).
// Production: JSON for machine-parseable logs.
// Development: human-readable single-line output with scopes.
builder.Logging.ClearProviders();
if (builder.Environment.IsProduction())
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.JsonWriterOptions = new() { Indented = false };
    });
}
else
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
    });
}

// CORS — origins are config-driven so the Vercel production URL can be added
// via environment variable without a code change:
//   Cors__AllowedOrigins__0=https://recruiter-ai.vercel.app
const string CorsPolicy = "AllowedOrigins";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

// Production CORS guard: fail fast on misconfiguration.
// Empty list silently disables CORS; "*" combined with credentials would
// turn the API into an open relay. Both are blocked at startup in Prod.
if (builder.Environment.IsProduction())
{
    if (allowedOrigins.Length == 0)
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must be set in Production (e.g. Cors__AllowedOrigins__0=https://app.example.com).");

    if (allowedOrigins.Any(o => o.Trim() == "*"))
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must not contain \"*\" in Production — list explicit origins.");
}

builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithOrigins(allowedOrigins));
});

// Rate limiting for OpenAI-cost endpoints (/screen, /generate).
// Fixed window: 10 requests per minute per remote IP — protects the OpenAI
// budget from runaway scripts. Single-user MVP scale; tighten before public deploy.
// TODO Production: replace IP partitioning with per-user/per-tenant partitioning
// once JWT auth is in place; consider distributed rate limiter (Redis) for multi-instance.
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("openai-cost", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0,
            }));
});

// ProblemDetails + global exception handler. Unhandled exceptions become
// RFC 7807 application/problem+json instead of leaking stack traces.
builder.Services.AddProblemDetails(opt =>
{
    opt.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Limit multipart uploads to 25 MB total; per-file limit enforced in CandidatesController.
// Kestrel MaxRequestBodySize is set to the same value so the server rejects
// oversized raw bodies before ASP.NET Core starts reading them.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 25L * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 25L * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger / OpenAPI document and UI.
builder.Services.AddSwaggerGen();

// Infrastructure: DbContext and (later) OpenAI clients.
builder.Services.AddInfrastructure(builder.Configuration);

// Health checks, including a PostgreSQL connectivity probe via AppDbContext.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres");

var app = builder.Build();

// Auto-apply EF migrations on startup for containerised single-instance deploys.
// TODO Production multi-instance: move to an explicit CI step (`dotnet ef database update`)
// to avoid two replicas racing on the same migration.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Recruiter AI v1");
        c.RoutePrefix = "swagger";
    });
}

// CORS before HTTPS redirect: preflight OPTIONS must get CORS headers
// before the 307 redirect fires, otherwise the browser aborts with a CORS error.
app.UseCors(CorsPolicy);
app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Global exception handler: converts unhandled exceptions to RFC 7807 ProblemDetails.
// .NET: IExceptionHandler is the .NET 8+ replacement for IExceptionFilter / UseExceptionHandler lambdas.
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception,
            "Unhandled exception. TraceId={TraceId} Path={Path}",
            httpContext.TraceIdentifier, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title  = "An unexpected error occurred.",
                Type   = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            },
        });
    }
}

// Exposes Program as a public type so WebApplicationFactory<Program> can
// reference it from the test project without InternalsVisibleTo.
public partial class Program { }
