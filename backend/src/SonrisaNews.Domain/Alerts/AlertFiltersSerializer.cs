using System.Text.Json;
using SonrisaNews.Domain;

namespace SonrisaNews.Domain.Alerts;

/// <summary>
/// Outcome of <see cref="AlertFiltersSerializer.TryDeserialize"/>. The
/// <see cref="Value"/> is the canonical JSON when the result is
/// <see cref="Success"/>; the <see cref="Errors"/> list is the field-level
/// validation messages otherwise.
/// </summary>
public sealed record AlertFiltersParseResult(
    bool Success,
    string? Value,
    IReadOnlyList<AlertFiltersError> Errors)
{
    public static AlertFiltersParseResult Ok(string canonical) =>
        new(Success: true, Value: canonical, Errors: Array.Empty<AlertFiltersError>());

    public static AlertFiltersParseResult Failed(IReadOnlyList<AlertFiltersError> errors) =>
        new(Success: false, Value: null, Errors: errors);
}

/// <summary>A single field-level validation error from <see cref="AlertFiltersSerializer"/>.</summary>
public sealed record AlertFiltersError(string Field, string Message);

/// <summary>
/// Typed serializer / deserializer for the <c>Alert.Filters</c> JSON
/// column. Reads per <see cref="AlertType"/> and rejects unknown
/// properties, wrong types, and structurally invalid input.
/// </summary>
/// <remarks>
/// <para>
/// The serializer is the schema-validity tripwire called out in
/// <c>mvp-checklist.md</c> §2 (wave 5): "Filters JSON is schema-validated
/// per type; unknown fields are rejected."
/// </para>
/// <para>
/// The output is the <b>canonical</b> JSON (re-serialized from the typed
/// DTO), so the <c>Filters</c> column on disk is always in the same
/// shape — property order, casing, and absent-vs-null — regardless of
/// the input's quirks. This keeps the matcher (waves 6–8) able to read
/// the column with a typed deserializer and get back the same object
/// every time.
/// </para>
/// </remarks>
public static class AlertFiltersSerializer
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Parse the raw <c>Filters</c> string for the given alert type and
    /// return the canonical JSON. The result is <c>Success</c> only when
    /// the input is well-formed JSON, has no unknown properties, and has
    /// no per-type structural errors (e.g. an empty <c>symbols</c> array
    /// on a market alert).
    /// </summary>
    /// <param name="alertType">The alert type the filter is for.</param>
    /// <param name="raw">The raw JSON string from the request body.</param>
    /// <returns>The canonical JSON on success; field-level errors otherwise.</returns>
    public static AlertFiltersParseResult TryDeserialize(AlertType alertType, string raw)
    {
        if (raw is null)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", "Filters must be a JSON object."),
            });
        }

        string trimmed = raw.Trim();

        // Reject non-object and unknown-property inputs before we
        // dispatch to the per-type validator. The per-type validator
        // also handles the empty-{} case naturally (all required
        // fields are reported as errors by Market/Disaster, and
        // News accepts the empty shape).
        JsonElement element;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            element = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", $"Filters is not valid JSON: {ex.Message}"),
            });
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", "Filters must be a JSON object."),
            });
        }

        var unknown = FindUnknownProperties(element, alertType);
        if (unknown.Count > 0)
        {
            var errors = unknown
                .Select(name => new AlertFiltersError(name, "Unknown field for this alert type."))
                .ToArray();
            return AlertFiltersParseResult.Failed(errors);
        }

        return alertType switch
        {
            AlertType.News => ValidateAndCanonicalizeNews(trimmed),
            AlertType.Market => ValidateAndCanonicalizeMarket(trimmed),
            AlertType.Disaster => ValidateAndCanonicalizeDisaster(trimmed),
            _ => AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("type", $"Unknown alert type: {alertType}"),
            }),
        };
    }

    private static List<string> FindUnknownProperties(JsonElement element, AlertType alertType)
    {
        HashSet<string> allowed = alertType switch
        {
            AlertType.News => new(StringComparer.Ordinal) { "sourceIds", "keyword", "matchMode", "tags" },
            AlertType.Market => new(StringComparer.Ordinal) { "symbols", "percentThreshold", "windowMinutes" },
            AlertType.Disaster => new(StringComparer.Ordinal) { "regions", "eventTypes", "minSeverity" },
            _ => new(StringComparer.Ordinal),
        };

        List<string> unknown = new();
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                unknown.Add(property.Name);
            }
        }
        return unknown;
    }

    private static AlertFiltersParseResult ValidateAndCanonicalizeNews(string raw)
    {
        NewsAlertFilters? typed;
        try
        {
            typed = JsonSerializer.Deserialize<NewsAlertFilters>(raw, ReadOptions);
        }
        catch (JsonException ex)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", $"Filters is not a valid News filter: {ex.Message}"),
            });
        }

        if (typed is null)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", "News filter is required."),
            });
        }

        // 0 = the C# default for the enum (= All when the wire format
        // omits the field on an empty-object request). Promote the
        // default to All so an empty filter is valid.
        if (typed.MatchMode == 0)
        {
            typed = typed with { MatchMode = KeywordMatchMode.All };
        }

        var errors = new List<AlertFiltersError>();

        if (typed.SourceIds is not null)
        {
            foreach (var id in typed.SourceIds)
            {
                if (id == Guid.Empty)
                {
                    errors.Add(new("sourceIds", "SourceIds cannot contain the empty Guid."));
                }
            }
        }

        if (typed.Keyword is { Length: > 200 })
        {
            errors.Add(new("keyword", "Keyword must be 200 characters or fewer."));
        }

        if (!Enum.IsDefined(typed.MatchMode))
        {
            errors.Add(new("matchMode", $"Unknown match mode: {(int)typed.MatchMode}."));
        }

        if (typed.Tags is not null)
        {
            for (int i = 0; i < typed.Tags.Count; i++)
            {
                var tag = typed.Tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    errors.Add(new($"tags[{i}]", "Tag cannot be blank."));
                }
                else if (tag.Length > 64)
                {
                    errors.Add(new($"tags[{i}]", "Tag must be 64 characters or fewer."));
                }
            }
        }

        return errors.Count == 0
            ? AlertFiltersParseResult.Ok(JsonSerializer.Serialize(typed, WriteOptions))
            : AlertFiltersParseResult.Failed(errors);
    }

    private static AlertFiltersParseResult ValidateAndCanonicalizeMarket(string raw)
    {
        MarketAlertFilters? typed;
        try
        {
            typed = JsonSerializer.Deserialize<MarketAlertFilters>(raw, ReadOptions);
        }
        catch (JsonException ex)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", $"Filters is not a valid Market filter: {ex.Message}"),
            });
        }

        if (typed is null)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", "Market filter is required."),
            });
        }

        var errors = new List<AlertFiltersError>();

        if (typed.Symbols is null || typed.Symbols.Count == 0)
        {
            errors.Add(new("symbols", "At least one symbol is required."));
        }
        else
        {
            for (int i = 0; i < typed.Symbols.Count; i++)
            {
                var symbol = typed.Symbols[i];
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    errors.Add(new($"symbols[{i}]", "Symbol cannot be blank."));
                }
                else if (symbol.Length > 16)
                {
                    errors.Add(new($"symbols[{i}]", "Symbol must be 16 characters or fewer."));
                }
            }
        }

        if (double.IsNaN(typed.PercentThreshold) || double.IsInfinity(typed.PercentThreshold))
        {
            errors.Add(new("percentThreshold", "Percent threshold must be a finite number."));
        }
        else if (typed.PercentThreshold <= 0)
        {
            errors.Add(new("percentThreshold", "Percent threshold must be positive."));
        }
        else if (typed.PercentThreshold > 1000)
        {
            errors.Add(new("percentThreshold", "Percent threshold must be 1000% or less."));
        }

        if (typed.WindowMinutes <= 0)
        {
            errors.Add(new("windowMinutes", "Window minutes must be a positive integer."));
        }
        else if (typed.WindowMinutes > 24 * 60)
        {
            errors.Add(new("windowMinutes", "Window minutes must be 1440 (one day) or less."));
        }

        return errors.Count == 0
            ? AlertFiltersParseResult.Ok(JsonSerializer.Serialize(typed, WriteOptions))
            : AlertFiltersParseResult.Failed(errors);
    }

    private static AlertFiltersParseResult ValidateAndCanonicalizeDisaster(string raw)
    {
        DisasterAlertFilters? typed;
        try
        {
            typed = JsonSerializer.Deserialize<DisasterAlertFilters>(raw, ReadOptions);
        }
        catch (JsonException ex)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", $"Filters is not a valid Disaster filter: {ex.Message}"),
            });
        }

        if (typed is null)
        {
            return AlertFiltersParseResult.Failed(new[]
            {
                new AlertFiltersError("filters", "Disaster filter is required."),
            });
        }

        var errors = new List<AlertFiltersError>();

        if (typed.Regions is null || typed.Regions.Count == 0)
        {
            errors.Add(new("regions", "At least one region is required."));
        }
        else
        {
            for (int i = 0; i < typed.Regions.Count; i++)
            {
                var region = typed.Regions[i];
                if (string.IsNullOrWhiteSpace(region))
                {
                    errors.Add(new($"regions[{i}]", "Region cannot be blank."));
                }
                else if (region.Length > 32)
                {
                    errors.Add(new($"regions[{i}]", "Region must be 32 characters or fewer."));
                }
            }
        }

        if (typed.EventTypes is null || typed.EventTypes.Count == 0)
        {
            errors.Add(new("eventTypes", "At least one event type is required."));
        }
        else
        {
            for (int i = 0; i < typed.EventTypes.Count; i++)
            {
                var type = typed.EventTypes[i];
                if (string.IsNullOrWhiteSpace(type))
                {
                    errors.Add(new($"eventTypes[{i}]", "Event type cannot be blank."));
                }
                else if (type.Length > 32)
                {
                    errors.Add(new($"eventTypes[{i}]", "Event type must be 32 characters or fewer."));
                }
            }
        }

        if (double.IsNaN(typed.MinSeverity) || double.IsInfinity(typed.MinSeverity))
        {
            errors.Add(new("minSeverity", "Min severity must be a finite number."));
        }
        else if (typed.MinSeverity < 0)
        {
            errors.Add(new("minSeverity", "Min severity must be zero or positive."));
        }

        return errors.Count == 0
            ? AlertFiltersParseResult.Ok(JsonSerializer.Serialize(typed, WriteOptions))
            : AlertFiltersParseResult.Failed(errors);
    }
}
