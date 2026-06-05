using Scalar.AspNetCore;
using Serilog;
using SonrisaNews.Infrastructure;
using SonrisaNews.Infrastructure.Auth;
using SonrisaNews.Shared;
using SonrisaNews.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---------------------------------------------------------------
// Serilog setup is shared with the Worker (see Shared/Hosting/SonrisaHostingExtensions)
// so both processes emit the same JSON log shape.
builder.Services.AddSonrisaNewsSerilog();

// --- Auth + RBAC ------------------------------------------------------------
// Bind AdminSeed options from the "AdminSeed" config section (which can be
// populated by the appsettings.Development.json block in dev, or by the
// AdminSeed__Email / AdminSeed__Password env vars in prod). AddSonrisaNewsAuth
// expects these to be bound by the caller because the env-var names are a
// host-level concern, not Infrastructure's.
builder.Services
    .Configure<AdminSeederOptions>(builder.Configuration.GetSection(AdminSeederOptions.SectionName));

builder.Services.AddSonrisaNewsInfrastructure();
builder.Services.AddSonrisaNewsAuth(builder.Configuration);
builder.Services.AddSonrisaNewsPolicies();

// --- Services --------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

// Auth middleware order matters: UseAuthentication populates the
// ClaimsPrincipal from the bearer token, UseAuthorization runs the
// [Authorize] attributes and the RbacPolicyHandler. Both are required
// even for the [AllowAnonymous] endpoints (the auth middleware is
// idempotent when there's no token).
app.UseAuthentication();
app.UseAuthorization();

// --- Liveness / readiness probes (no business logic) -----------------------
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
   .WithSummary("Liveness probe")
   .WithDescription("Returns 200 if the API process is running. No dependency checks.")
   .Produces(StatusCodes.Status200OK);

app.MapControllers();

app.Run();

/// <summary>Marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
