---
name: 'add-a-data-source'
description: 'Step-by-step checklist for adding a new IDataSource (a new RSS feed, a new disaster feed, a new market symbol group). Use when the user says "add a <X> source" or "polling a new feed".'
---

# Add a Data Source

You are adding a new `IDataSource` implementation — a new pollable upstream (RSS, USGS, GDACS, NHC, etc.) or a new logical source within an existing upstream (e.g. a new RSS feed, a new earthquake magnitude band).

## 1. Pre-flight

- Read `AGENTS.md`.
- Read `.github/copilot-instructions.md` — the "data source" tripwire.
- Read `.github/instructions/csharp-dotnet.instructions.md`.
- Read the existing `RssSource.cs`, `UsgsEarthquakeSource.cs`, etc. in `backend/src/SonrisaNews.Infrastructure/Sources/`. They are the templates.
- Confirm with the user: which source? What does the upstream return? Is there a rate limit? Is there an auth requirement (most should be none for our free sources)?

## 2. Implement the source

Create the new class. Example for an RSS feed:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SonrisaNews.Domain.Sources;

namespace SonrisaNews.Infrastructure.Sources;

public sealed class HackerNewsSource : IDataSource
{
    public string Id => "hackernews-top";  // stable id; used in admin, log lines, and the Event.SourceId

    public SourceType Type => SourceType.News;

    public TimeSpan PollInterval => TimeSpan.FromMinutes(2);

    public HackerNewsSource(/* inject an HttpClient via the typed-client factory */) { }

    public async Task<IReadOnlyList<RawEvent>> FetchAsync(CancellationToken ct)
    {
        // 1. Fetch the upstream (HttpClient, NOT a static method)
        // 2. Parse the response into RawEvent instances
        // 3. Return the list. The poller dedupes by RawEvent.ExternalId.
        throw new NotImplementedException();
    }
}
```

Rules:

- **`Id` is stable** — once it's in the DB, do not change it. Renaming breaks event dedup.
- **`PollInterval` is the *desired* interval**; the actual scheduling is a worker concern.
- **`Type`** is one of `SourceType.News`, `SourceType.Disaster`, `SourceType.MarketSymbol`.
- **Use a typed `HttpClient`** (registered via `AddHttpClient<HackerNewsSource>(...)`), not a static method or a service-locator.
- **Cancellation flows through.** Every `await` gets the `ct`.
- **All exceptions are caught and converted** to an empty list (with a log). A transient upstream failure must not crash the worker; the next poll will retry.
- **Dedup is the source's job.** Compute a stable `ExternalId` from the upstream's identifier (GUID for RSS, USGS event id for earthquakes, etc.).

## 3. Register in DI

Edit `backend/src/SonrisaNews.Worker/Program.cs` (or the worker's DI module):

```csharp
builder.Services.AddHttpClient<HackerNewsSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "SonrisaNews/1.0");
});
builder.Services.AddSingleton<IDataSource, HackerNewsSource>();
```

`IDataSource` is a singleton because the poller iterates instances once per loop.

## 4. Seed the source (default-on, optional)

If the source should be on by default, add a row to the seed in `backend/src/SonrisaNews.Infrastructure/Persistence/Seeds/SourcesSeed.cs`:

```csharp
new Source
{
    Id = "hackernews-top",
    Type = SourceType.News,
    Name = "Hacker News — Top Stories",
    Enabled = true,
    Config = /* any per-source config as JSON */,
}
```

If the user is opting in, the admin adds the source via the admin UI; no seed row.

## 5. Admin UI entry (out-of-band)

If the source is admin-managed and the user adds/removes it via the UI, the TDD Next.js Implementer agent will need to add a row to the admin "Sources" page. Hand that off as a follow-up.

## 6. Tests (TDD, red-green-refactor)

For every source you must have **at least two tests** in `backend/tests/SonrisaNews.UnitTests/Sources/HackerNewsSourceTests.cs`:

1. **`FetchAsync` returns the expected `RawEvent` shape** for a canned upstream response.
2. **`FetchAsync` returns an empty list** (does not throw) when the upstream returns a 5xx.

Use a fake `HttpMessageHandler` that returns a canned string. Assert on the parsed `RawEvent` fields, not on the raw JSON.

## 7. Verify

Run from `backend/`:

```bash
dotnet test --filter FullyQualifiedName~HackerNewsSource
```

Then a full `dotnet test`.

## 8. Hand off

Open a PR with:

- The new source class
- The DI registration
- The seed row (if applicable)
- The two tests
- A note for the admin UI: "source should appear in the admin Sources list"

The Backend Reviewer agent reviews. The user merges.

## Forbidden

- Implementing a source without a `FetchAsync` test
- Static HTTP calls inside the source
- Hard-coding a base URL that should be configurable
- Changing `Id` after the source is in the DB (renaming creates a new source in the user's mind but orphans the events in the DB)
- Letting an exception escape `FetchAsync`
- `Thread.Sleep` between fetches (the poller handles the interval)
- Auth in the source (use an upstream that doesn't require it; the only auth we accept is "user-supplied API key in the admin UI", and that goes through a different mechanism)
