---
name: 'add-a-channel'
description: 'Step-by-step checklist for adding a new INotificationChannel (e.g. Discord, SMS, Webhook, Push). Use when the user says "add a <X> channel" or "implement a new notification channel".'
---

# Add a Channel

You are adding a new `INotificationChannel` implementation. Follow this checklist in order. **Do not skip steps. Do not reorder.**

## 1. Pre-flight (always)

- Read `AGENTS.md`.
- Read `.github/copilot-instructions.md` — the "channel" tripwire.
- Read `.github/instructions/csharp-dotnet.instructions.md`.
- Read the existing `EmailChannel.cs` and `SlackChannel.cs` in `backend/src/SonrisaNews.Infrastructure/Channels/`. They are the templates.
- Confirm with the user: which channel (Discord, SMS, Webhook, Push, …)? What does the destination look like (URL, phone number, token)?

## 2. Define the channel type constant

Edit `backend/src/SonrisaNews.Domain/Notifications/ChannelType.cs` (or create it if missing):

```csharp
public static class ChannelType
{
    public const string Email = "email";
    public const string Slack = "slack";
    // New:
    public const string Discord = "discord";
}
```

The string value is what goes in the DB. Match the lowercase name of the channel.

## 3. Implement the channel

Create `backend/src/SonrisaNews.Infrastructure/Channels/DiscordChannel.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SonrisaNews.Domain.Notifications;

namespace SonrisaNews.Infrastructure.Channels;

public sealed class DiscordChannel : INotificationChannel
{
    public string Type => ChannelType.Discord;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscordChannel> _logger;

    public DiscordChannel(IHttpClientFactory httpClientFactory, ILogger<DiscordChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<VerificationChallenge> StartVerificationAsync(
        string destination, CancellationToken ct)
    {
        // 1. Send a test message to the destination (e.g. "Your Sonrisa verification code is 123456")
        // 2. Return a challenge that the user must echo back to confirm
        throw new NotImplementedException();
    }

    public async Task<bool> VerifyAsync(
        string destination, string code, CancellationToken ct)
    {
        // Compare the code to the one issued in StartVerificationAsync
        throw new NotImplementedException();
    }

    public async Task<SendResult> SendAsync(
        NotificationPayload payload, CancellationToken ct)
    {
        // POST to the Discord webhook with the formatted message
        throw new NotImplementedException();
    }
}
```

Rules:

- **Inject `IHttpClientFactory`.** Never `new HttpClient()`.
- **Inject `ILogger<T>`.** Never `Console.WriteLine`.
- **No PII in logs.** Log the `alertId` and `notificationId`, not the destination URL with a token in it.
- **Cancellation flows through.** Every `await` gets the `ct`.
- **All exceptions are typed.** Map to a `SendResult.Failure(errorCode, errorMessage)`. Never let an exception escape.

## 4. Register in DI

Edit `backend/src/SonrisaNews.Api/Program.cs` (and the Worker's `Program.cs` if it dispatches notifications):

```csharp
builder.Services.AddKeyedScoped<INotificationChannel, DiscordChannel>(ChannelType.Discord);
```

(For an MVP channel, `AddKeyedScoped` is fine. If the channel holds expensive state, switch to `AddKeyedSingleton`.)

## 5. Update the `Channel.Type` column constraint (if any)

If the `Channel` table has a check constraint on `Type`, add the new value. SQLite doesn't enforce check constraints by default, so this is usually a no-op; Postgres will need an explicit migration.

## 6. Add an icon for the frontend (out-of-band)

Tell the user: "The frontend renders a channel icon based on `Channel.Type`. Add an entry to `web/lib/channels/icons.tsx` (or equivalent). The TDD Next.js Implementer agent will pick this up as a follow-up."

## 7. Tests (TDD, red-green-refactor)

For every channel you must have **at least four tests** in `backend/tests/SonrisaNews.UnitTests/Channels/DiscordChannelTests.cs`:

1. **Happy path `SendAsync`** — the channel posts the message and returns `SendResult.Success`.
2. **`SendAsync` when the upstream is down** — returns `SendResult.Failure` with a typed error code; no exception escapes.
3. **Happy path `VerifyAsync`** — the correct code is accepted.
4. **`VerifyAsync` with a wrong code** — the incorrect code is rejected.

Use a fake `HttpMessageHandler` for the HTTP client. Never hit Discord in tests.

## 8. Verify

Run from `backend/`:

```bash
dotnet test --filter FullyQualifiedName~DiscordChannel
```

All four tests must pass. Then a full `dotnet test` to confirm no regressions.

## 9. Hand off

Open a PR with:

- The new `DiscordChannel.cs`
- The DI registration
- The `ChannelType` constant
- The four tests
- A note for the frontend: "icon needed in `web/lib/channels/icons.tsx`"

The Backend Reviewer agent reviews. The user merges.

## Forbidden

- Implementing a channel without a `SendAsync` test
- Implementing a channel without a `VerifyAsync` test
- Hard-coding a webhook URL in the channel
- Logging the destination URL (webhooks carry secrets in the URL)
- Letting an exception escape `SendAsync` (must return a `SendResult.Failure`)
- Skipping the DI registration
- Editing `appsettings.*.json` to add a "Discord API key" (channels don't use API keys; they use user-supplied destinations)
