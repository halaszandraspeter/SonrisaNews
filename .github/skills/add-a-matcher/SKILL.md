---
name: 'add-a-matcher'
description: 'Step-by-step checklist for adding a new filter type to the alert-matching engine (e.g. a new News filter, a new Market trigger). Use when the user says "support <X> in alerts" or "add a new match condition".'
---

# Add a Matcher

You are extending the alert-matching engine with a new filter type. A filter is a typed predicate that takes an `Event` and a filter-specific configuration, and returns `bool` (does this event match?).

## 1. Pre-flight

- Read `AGENTS.md`.
- Read `.github/copilot-instructions.md` — the OpenAPI / schema tripwires.
- Read `.github/instructions/csharp-dotnet.instructions.md`.
- Read the existing matchers in `backend/src/SonrisaNews.Worker/Matcher/`. They are the templates.
- Read `Alert` and `Event` in the domain layer; your filter operates on these.
- Confirm with the user: which matcher (e.g. "news keyword with AND mode", "market absolute price threshold")? What's the data shape?

## 2. Define the filter DTO

Add a new typed record for the filter. The filter is **part of** the alert's `Filters` JSON, but each filter type has a strongly-typed DTO that the matcher uses.

```csharp
namespace SonrisaNews.Domain.Alerts.Filters;

public sealed record NewsTopicTagFilter(
    IReadOnlyList<string> Tags,
    bool RequireAll);  // AND vs OR
```

Rules:

- **One record per filter type.** Don't reuse a record across types.
- **`init`-only setters.** Filters are immutable.
- **No nullable collections.** Empty list = "filter is off", not "no filter applied". This keeps the matcher logic simple.

## 3. Define a marker interface for the filter (if you need polymorphic dispatch)

Most filters are simple predicates. If your filter is one of N for a given alert type, you can:

- **Option A** (simple): add a `Tag` enum + a `switch` in the matcher. Easy to read, easy to test.
- **Option B** (open for extension): define an `INewsFilter` interface, register implementations in DI, resolve by `Tag`. More plumbing, but easier to add filters without touching the matcher core.

Default: **Option A** for MVP. Switch to Option B when there are > 3 filter types per alert type.

## 4. Implement the matcher predicate

Add a method to the alert-type's matcher class:

```csharp
public bool Matches(NewsTopicTagFilter filter, Event @event)
{
    if (filter.Tags.Count == 0) return true;  // no filter, match all

    var eventTags = @event.Payload.GetProperty("tags")
        .EnumerateArray()
        .Select(t => t.GetString() ?? string.Empty)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return filter.RequireAll
        ? filter.Tags.All(t => eventTags.Contains(t))
        : filter.Tags.Any(t => eventTags.Contains(t));
}
```

Rules:

- **Pure function.** No I/O, no time, no random. (This is what makes it testable.)
- **Cancellation is not needed** — there's no I/O. The matcher is in-process.
- **Empty filter = match all.** This is a deliberate default; the alert UI shows the filter is "off".
- **Case sensitivity is the filter's choice**, encoded in the predicate.

## 5. Wire the filter into the alert's Filters JSON

The `Alert` entity's `Filters` is a `JsonDocument`. You need to add a property to the schema:

```json
{
  "sourceIds": ["..."],
  "keyword": "...",
  "matchMode": "any",
  "tags": ["ai", "climate"],
  "requireAll": false
}
```

Update the OpenAPI DTOs to expose the new field. Update the frontend's form to capture it.

## 6. Tests (TDD, red-green-refactor)

In `backend/tests/SonrisaNews.UnitTests/Matcher/NewsMatcherTests.cs`, add tests for the new filter:

1. **Match: any-tag-match (OR mode)**, event with one matching tag → matches.
2. **Match: all-tag-match (AND mode)**, event with all matching tags → matches.
3. **No match: OR mode**, event with no matching tags → does not match.
4. **No match: AND mode**, event missing one tag → does not match.
5. **Empty filter**, event with no tags → matches (default).
6. **Case insensitivity**, `AI` vs `ai` → matches.

For market alerts, the predicate is a property of the quote history. Add a parallel set of tests for the market matcher.

## 7. Verify

Run from `backend/`:

```bash
dotnet test --filter FullyQualifiedName~NewsMatcher
```

Then a full `dotnet test`.

## 8. Update the OpenAPI doc

The new filter field appears in the request DTO for alert creation. Update the DTO record, the controller's `[ProducesResponseType]`, and run the codegen:

```bash
cd web
pnpm generate:api
```

Commit the regenerated client. (The CI drift check will fail if you don't.)

## 9. Update the frontend form

Hand off to the TDD Next.js Implementer agent: "Add `<field>` to the news-alert form, with the Zod schema and a Vitest test for the predicate behavior."

## 10. Hand off

Open a PR with:

- The new filter DTO
- The matcher predicate
- The tests
- The OpenAPI DTO update
- A note for the frontend: "form field needed"

The Backend Reviewer reviews. The user merges.

## Forbidden

- A filter that does I/O (no DB lookups, no HTTP calls, no clock reads)
- A filter that's stateful (it must be a pure function of the event and the filter config)
- An empty filter that doesn't match all (the convention is "empty = off = match all")
- Adding a filter without updating the OpenAPI DTO
- Adding a filter without updating the frontend form (drift)
- A matcher test that depends on the time of day
- A matcher test that depends on a specific test execution order
