using FluentAssertions;
using SonrisaNews.Domain;
using SonrisaNews.Domain.Alerts;
using Xunit;

namespace SonrisaNews.UnitTests.Alerts;

/// <summary>
/// Tests for <see cref="AlertFiltersSerializer"/>. The serializer is the
/// schema-validity tripwire called out in
/// <c>docs/implementation/mvp-checklist.md</c> §2 (wave 5):
/// "Filters JSON is schema-validated per type; unknown fields are rejected."
/// </summary>
[Trait("Category", AlertsTestCategory.Serializer)]
public class AlertFiltersSerializerTests
{
    // -- News ----------------------------------------------------------------

    [Fact]
    public void TryDeserialize_NewsWithKeywordAndSourceIds_ReturnsCanonical()
    {
        // The canonical output drops the input's "matchMode" default
        // re-encoding (we still emit it as the integer value) but
        // normalizes casing and shape.
        var raw = """{"sourceIds":["11111111-1111-1111-1111-111111111111"],"keyword":"brexit","matchMode":"All","tags":[]}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, raw);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        // The canonical output is valid JSON; the test asserts the
        // structure rather than byte-equality (the property order in the
        // canonical output is the record's declaration order, not the
        // input's).
        using var doc = System.Text.Json.JsonDocument.Parse(result.Value!);
        var root = doc.RootElement;
        root.GetProperty("keyword").GetString().Should().Be("brexit");
        root.GetProperty("matchMode").GetInt32().Should().Be((int)KeywordMatchMode.All);
    }

    [Fact]
    public void TryDeserialize_NewsWithUnknownField_ReturnsError()
    {
        var raw = """{"keyword":"brexit","somethingElse":42}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "somethingElse");
    }

    [Fact]
    public void TryDeserialize_NewsWithEmptyObject_Succeeds()
    {
        // An empty News filter means "match everything" — a valid state.
        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, "{}");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void TryDeserialize_NewsWithKeywordTooLong_ReturnsError()
    {
        var longKeyword = new string('a', 201);
        var raw = "{\"keyword\":\"" + longKeyword + "\"}";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "keyword");
    }

    [Fact]
    public void TryDeserialize_NewsWithEmptySourceId_ReturnsError()
    {
        var raw = """{"sourceIds":["00000000-0000-0000-0000-000000000000"]}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "sourceIds");
    }

    [Fact]
    public void TryDeserialize_NewsWithBlankTag_ReturnsError()
    {
        var raw = """{"tags":["valid","   "]}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "tags[1]");
    }

    // -- Market --------------------------------------------------------------

    [Fact]
    public void TryDeserialize_MarketWithSymbolsAndThreshold_ReturnsCanonical()
    {
        var raw = """{"symbols":["AAPL","MSFT"],"percentThreshold":5.0,"windowMinutes":60}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryDeserialize_MarketWithoutSymbols_ReturnsError()
    {
        var raw = """{"percentThreshold":5.0,"windowMinutes":60}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "symbols");
    }

    [Fact]
    public void TryDeserialize_MarketWithEmptySymbolsArray_ReturnsError()
    {
        var raw = """{"symbols":[],"percentThreshold":5.0,"windowMinutes":60}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "symbols");
    }

    [Fact]
    public void TryDeserialize_MarketWithNegativeThreshold_ReturnsError()
    {
        var raw = """{"symbols":["AAPL"],"percentThreshold":-1.0,"windowMinutes":60}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "percentThreshold");
    }

    [Fact]
    public void TryDeserialize_MarketWithZeroWindow_ReturnsError()
    {
        var raw = """{"symbols":["AAPL"],"percentThreshold":5.0,"windowMinutes":0}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "windowMinutes");
    }

    [Fact]
    public void TryDeserialize_MarketWithUnknownField_ReturnsError()
    {
        var raw = """{"symbols":["AAPL"],"percentThreshold":5.0,"windowMinutes":60,"priceTarget":200.0}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "priceTarget");
    }

    // -- Disaster ------------------------------------------------------------

    [Fact]
    public void TryDeserialize_DisasterWithRegionsAndSeverity_ReturnsCanonical()
    {
        var raw = """{"regions":["JP","US-CA"],"eventTypes":["earthquake"],"minSeverity":5.0}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Disaster, raw);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryDeserialize_DisasterWithoutEventTypes_ReturnsError()
    {
        var raw = """{"regions":["JP"],"minSeverity":5.0}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Disaster, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "eventTypes");
    }

    [Fact]
    public void TryDeserialize_DisasterWithNegativeSeverity_ReturnsError()
    {
        var raw = """{"regions":["JP"],"eventTypes":["earthquake"],"minSeverity":-1.0}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Disaster, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "minSeverity");
    }

    [Fact]
    public void TryDeserialize_DisasterWithUnknownField_ReturnsError()
    {
        var raw = """{"regions":["JP"],"eventTypes":["earthquake"],"minSeverity":5.0,"daysBack":7}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Disaster, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "daysBack");
    }

    // -- Cross-type & bad input --------------------------------------------

    [Fact]
    public void TryDeserialize_NewsShapeRejectedOnMarketAlert()
    {
        // The "keyword" field is for News; a Market alert must not accept
        // it. (The unknown-property check is type-aware.)
        var raw = """{"keyword":"brexit"}""";

        var result = AlertFiltersSerializer.TryDeserialize(AlertType.Market, raw);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "keyword");
    }

    [Fact]
    public void TryDeserialize_MalformedJson_ReturnsError()
    {
        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, "{not json");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "filters");
    }

    [Fact]
    public void TryDeserialize_NonObjectJson_ReturnsError()
    {
        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, "[1,2,3]");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "filters");
    }

    [Fact]
    public void TryDeserialize_NullString_ReturnsError()
    {
        var result = AlertFiltersSerializer.TryDeserialize(AlertType.News, null!);

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "filters");
    }
}
