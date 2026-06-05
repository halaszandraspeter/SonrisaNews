namespace SonrisaNews.Shared;

/// <summary>Names of configuration sections. Centralized so a typo in <c>builder.Configuration.GetSection(...)</c> fails fast at compile time, not at runtime.</summary>
public static class ConfigurationKeys
{
    public const string ConnectionStrings = "ConnectionStrings";
    public const string SonrisaDatabase = "Sonrisa";
    public const string Jwt = "Jwt";
    public const string Smtp = "Smtp";
    public const string Yfinance = "Yfinance";

    // Bootstrap admin seed: env-var form is `AdminSeed__Email` /
    // `AdminSeed__Password` / `AdminSeed__DisplayName` (ASP.NET Core
    // maps "__" to ":" when reading env vars). The bound section name
    // is `AdminSeed`; see SonrisaNews.Infrastructure.Auth.AdminSeederOptions
    // for the section-name constant.
}
