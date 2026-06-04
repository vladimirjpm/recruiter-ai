using Microsoft.AspNetCore.Http.Features;
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

builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithOrigins(allowedOrigins));
});

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

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Exposes Program as a public type so WebApplicationFactory<Program> can
// reference it from the test project without InternalsVisibleTo.
public partial class Program { }
