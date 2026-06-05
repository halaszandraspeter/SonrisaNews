import { describe, expect, it } from "vitest";

import {
  canonicalize,
  disasterFiltersSchema,
  filterSchemaFor,
  isTypeFilterEmpty,
  marketFiltersSchema,
  newsFiltersSchema,
  parseFilters,
} from "./filterSchemas";

describe("newsFiltersSchema", () => {
  it("accepts an empty filter document", () => {
    const result = newsFiltersSchema.safeParse({
      sourceIds: [],
      keyword: null,
      matchMode: "All",
      tags: [],
    });
    expect(result.success).toBe(true);
  });

  it("accepts a populated filter document", () => {
    const result = newsFiltersSchema.safeParse({
      sourceIds: ["a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"],
      keyword: "earthquake",
      matchMode: "Any",
      tags: ["breaking"],
    });
    expect(result.success).toBe(true);
  });

  it("rejects an unknown property", () => {
    const result = newsFiltersSchema.safeParse({
      sourceIds: [],
      keyword: null,
      matchMode: "All",
      tags: [],
      surprise: true,
    });
    expect(result.success).toBe(false);
  });

  it("rejects a non-uuid sourceId", () => {
    const result = newsFiltersSchema.safeParse({
      sourceIds: ["not-a-uuid"],
      keyword: null,
      matchMode: "All",
      tags: [],
    });
    expect(result.success).toBe(false);
  });
});

describe("marketFiltersSchema", () => {
  it("accepts a valid market filter", () => {
    const result = marketFiltersSchema.safeParse({
      symbols: ["AAPL", "MSFT"],
      percentThreshold: 5,
      windowMinutes: 60,
    });
    expect(result.success).toBe(true);
  });

  it("rejects an empty symbol list", () => {
    const result = marketFiltersSchema.safeParse({
      symbols: [],
      percentThreshold: 5,
      windowMinutes: 60,
    });
    expect(result.success).toBe(false);
  });

  it("rejects a non-positive threshold", () => {
    const result = marketFiltersSchema.safeParse({
      symbols: ["AAPL"],
      percentThreshold: 0,
      windowMinutes: 60,
    });
    expect(result.success).toBe(false);
  });

  it("rejects a fractional window", () => {
    const result = marketFiltersSchema.safeParse({
      symbols: ["AAPL"],
      percentThreshold: 5,
      windowMinutes: 60.5,
    });
    expect(result.success).toBe(false);
  });

  it("rejects a window longer than 24 hours", () => {
    const result = marketFiltersSchema.safeParse({
      symbols: ["AAPL"],
      percentThreshold: 5,
      windowMinutes: 24 * 60 + 1,
    });
    expect(result.success).toBe(false);
  });
});

describe("disasterFiltersSchema", () => {
  it("accepts a valid disaster filter", () => {
    const result = disasterFiltersSchema.safeParse({
      regions: ["JP", "US-CA"],
      eventTypes: ["earthquake"],
      minSeverity: 5,
    });
    expect(result.success).toBe(true);
  });

  it("rejects an empty regions list", () => {
    const result = disasterFiltersSchema.safeParse({
      regions: [],
      eventTypes: ["earthquake"],
      minSeverity: 5,
    });
    expect(result.success).toBe(false);
  });

  it("rejects an unknown event type", () => {
    const result = disasterFiltersSchema.safeParse({
      regions: ["JP"],
      eventTypes: ["meteor"],
      minSeverity: 5,
    });
    expect(result.success).toBe(false);
  });

  it("rejects a negative severity", () => {
    const result = disasterFiltersSchema.safeParse({
      regions: ["JP"],
      eventTypes: ["earthquake"],
      minSeverity: -1,
    });
    expect(result.success).toBe(false);
  });
});

describe("filterSchemaFor", () => {
  it("returns the News schema for News", () => {
    expect(filterSchemaFor("News")).toBe(newsFiltersSchema);
  });

  it("returns the Market schema for Market", () => {
    expect(filterSchemaFor("Market")).toBe(marketFiltersSchema);
  });

  it("returns the Disaster schema for Disaster", () => {
    expect(filterSchemaFor("Disaster")).toBe(disasterFiltersSchema);
  });
});

describe("parseFilters", () => {
  it("parses a valid News filter string", () => {
    const raw = JSON.stringify({ sourceIds: [], keyword: null, matchMode: "All", tags: [] });
    const result = parseFilters("News", raw);
    expect(result.success).toBe(true);
  });

  it("returns a friendly error for invalid JSON", () => {
    const result = parseFilters("News", "{ not json");
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.message.length).toBeGreaterThan(0);
    }
  });

  it("returns a friendly error for a structural error", () => {
    const raw = JSON.stringify({ symbols: [], percentThreshold: 5, windowMinutes: 60 });
    const result = parseFilters("Market", raw);
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.message.toLowerCase()).toContain("symbol");
    }
  });
});

describe("canonicalize", () => {
  it("round-trips a valid News filter to canonical JSON", () => {
    const raw = JSON.stringify({ sourceIds: [], keyword: null, matchMode: "All", tags: [] });
    const result = parseFilters("News", raw);
    if (!result.success) throw new Error("setup");
    const canonical = canonicalize("News", result.data);
    // The canonical form must re-parse cleanly.
    const reparsed = parseFilters("News", canonical);
    expect(reparsed.success).toBe(true);
  });
});

describe("isTypeFilterEmpty", () => {
  it("returns false for News (any shape is valid)", () => {
    const result = parseFilters(
      "News",
      JSON.stringify({ sourceIds: [], keyword: null, matchMode: "All", tags: [] }),
    );
    if (!result.success) throw new Error("setup");
    expect(isTypeFilterEmpty("News", result.data)).toBe(false);
  });

  it("returns true for Market with no symbols", () => {
    const raw = JSON.stringify({ symbols: ["AAPL"], percentThreshold: 5, windowMinutes: 60 });
    const result = parseFilters("Market", raw);
    if (!result.success) throw new Error("setup");
    // Construct a "no symbols" variant
    const empty = { ...(result.data as { symbols: string[] }), symbols: [] };
    expect(isTypeFilterEmpty("Market", empty as never)).toBe(true);
  });

  it("returns true for Disaster with no regions", () => {
    const raw = JSON.stringify({ regions: ["JP"], eventTypes: ["earthquake"], minSeverity: 5 });
    const result = parseFilters("Disaster", raw);
    if (!result.success) throw new Error("setup");
    const empty = { ...(result.data as { regions: string[] }), regions: [] };
    expect(isTypeFilterEmpty("Disaster", empty as never)).toBe(true);
  });
});
