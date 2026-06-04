namespace SonrisaNews.Shared;

/// <summary>Names of configuration sections. Centralized so a typo in <c>builder.Configuration.GetSection(...)</c> fails fast at compile time, not at runtime.</summary>
public static class ConfigurationKeys
{
    public const string ConnectionStrings = "ConnectionStrings";
    public const string SonrisaDatabase = "Sonrisa";
    public const string Jwt = "Jwt";
    public const string Smtp = "Smtp";
    public const string Yfinance = "Yfinance";
    public const string SeedAdminEmail = "SEED_ADMIN_EMAIL";
    public const string SeedAdminPassword = "SEED_ADMIN_PASSWORD";
    public const string SeedAdminDisplayName = "SEED_ADMIN_DISPLAY_NAME";
}
