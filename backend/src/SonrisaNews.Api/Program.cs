using Scalar.AspNetCore;
using Serilog;
using SonrisaNews.Infrastructure;
using SonrisaNews.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---------------------------------------------------------------
// Serilog setup is shared with the Worker (see Shared/Hosting/SonrisaHostingExtensions)
// so both processes emit the same JSON log shape.
builder.Services.AddSonrisaNewsSerilog();

// --- Services --------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSonrisaNewsInfrastructure();

var app = builder.Build();

// --- OpenAPI / Scalar UI ---------------------------------------------------
// Dev-only. In production, OpenAPI is disabled (set OpenApi:Enabled=false in
// appsettings.Production.json) and this block is skipped. Do NOT widen
// IsDevelopment() to include Staging without also flipping that flag.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Sonrisa News API";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

app.UseSerilogRequestLogging();

// --- Liveness / readiness probes (no business logic) -----------------------
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
   .WithSummary("Liveness probe")
   .WithDescription("Returns 200 if the API process is running. No dependency checks.")
   .Produces(StatusCodes.Status200OK);

app.MapControllers();

app.Run();

/// <summary>Marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;