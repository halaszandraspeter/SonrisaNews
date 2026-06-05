# Sonrisa News — Features (MVP, 24-hour scope)

> **Product**: Sonrisa News
> **Type**: Public B2C. Free, open-source utility for the benefit of humanity.
> **Brief**: Users set up alerts that fire when important things happen in the world — breaking news, market movements, natural disasters. Delivered by email and Slack, with a channel abstraction so more channels can be added later. An admin view is required.
> **Document status**: Proposal. User has final word on every decision.

---

## 1. Non-goals (24-hour MVP)

To stay within 24h, we deliberately **cut** these from MVP. They appear on the roadmap, not in MVP.

- Native iOS / Android apps (web-only, responsive).
- Google / Apple / social sign-in (email/password only).
- Paid data providers (premium stock feeds, Bloomberg, Reuters, USGS enterprise).
- User-submitted RSS feeds (admin-managed only in MVP; user-request flow on roadmap).
- Push notifications, SMS, Discord, Microsoft Teams, webhooks (interface ready, only email + Slack implemented).
- Donations / payments (an "announcement" admin feature is in; payments are not).
- Multi-language UI (English only in MVP; copy lives in i18n keys from day 1 so adding locales is mechanical).
- Mobile app push (web push is a possible post-MVP stretch).

---

## 2. User-facing features (MVP)

### 2.1 Accounts

- **Email/password sign-up** with email verification.
- **Password reset** via signed token (24h expiry).
- **Session management**: JWT (access 15m) + refresh token (30d, httpOnly cookie).
- **Account deletion** (soft-delete, 30-day grace period).
- **Profile**: display name, time zone (used to render digest times correctly), preferred language.
- **No social login in MVP.** Hooks in `AuthProvider` interface so Google/Apple can drop in without a schema change.

### 2.2 Onboarding wizard (post-registration)

A 3-choice landing step. Whichever path the user picks, the wizard ends in a populated "My Alerts" dashboard.

| Step | What happens |
|---|---|
| **1. Choose path** | Three options: (a) AI-assisted setup, (b) manual setup, (c) skip → go with sensible defaults. |
| **2a. AI path** | Conversational UI: ~5 short questions ("Which regions matter to you?", "Industries you follow?", "Markets you watch?"). Backend calls `OnboardingAiService` which returns a starter set of alert suggestions. User reviews and accepts/edits. |
| **2b. Manual path** | User lands on alert-builder directly with empty state and the three category tiles (News, Markets, Disasters). |
| **2c. Skip** | User is given 3 default alerts (one of each category) and lands on the dashboard. |
| **3. Channel setup** | User configures at least one channel (email is required; Slack optional). The user can change delivery mode per channel. |
| **4. Done** | Confetti-free dashboard. |

Defaults (path c) ship as a JSON file the admin can edit, so the "skip" path stays curated.

### 2.3 Alerts (the core domain)

Alerts have a **type** (News, Market, Disaster) and **filters** specific to that type. Each alert has one or more **channel subscriptions** that say "send this alert to channel X in mode Y".

#### 2.3.1 News alerts

Multi-feature filter. An alert can use any combination of:

- **Per-source subscription**: pick from the admin-curated list of RSS feeds (e.g. Reuters World, BBC Tech, AP Top News). Multiple sources allowed.
- **Keyword filter**: comma-separated keywords with AND/OR modes; case-insensitive; matches against title + summary.
- **Common topic tags**: tags from a curated taxonomy (e.g. `politics`, `ai`, `climate`, `startups`). Multi-select.

Optional AI assist:
- "Describe what you care about" text box on the alert form. Sends to `OnboardingAiService` and returns a suggested filter set the user can accept or modify. Opt-in per alert.

#### 2.3.2 Market alerts

Trigger types in MVP:
- **% change in time window**: e.g. "AAPL moves 5% within any rolling 1-hour window". Symbol list, threshold %, window length (5m / 15m / 1h / 1d).

Out of scope for MVP: absolute price threshold, volume spike, symbol news. (Roadmap.)

A market alert uses **yfinance** quotes via a Python sidecar (see `2-stack.md`). Quotes are 15-minute delayed. This is documented in the UI ("data delayed up to 15 minutes"). We do **not** promise real-time in MVP.

#### 2.3.3 Disaster alerts

Filter dimensions:
- **Geographic region**: country list, US state list, or "global". Multiple allowed.
- **Event type**: earthquake, tsunami, hurricane/typhoon, volcano, wildfire, flood.
- **Severity threshold**: per-event-type minimum magnitude/category (e.g. `M5.0+` for earthquake, `Cat 3+` for hurricane). Defaults are sensible.

Data sources: USGS (earthquakes), GDACS (global), NHC (hurricanes). Free, no API key.

### 2.4 Channels & delivery

A **channel** is a user's connection to an external destination. Each user can have one or more channels:

- **Email**: SMTP target (default = signup email; user can add more). Verified via a **confirmation code sent to the address** — the user pastes the code (or clicks the link) to confirm. We never send real alerts to an unverified address.
- **Slack**: incoming-webhook URL. Verified via a test message on save.

**Quiet hours** (per channel): user can set a start/end time (e.g. "no Slack between 22:00 and 07:00 local"). Real-time matches that fire inside the quiet window are queued and sent at the next allowed moment; digest modes are unaffected. Stored as `Channel.QuietHoursStart` / `Channel.QuietHoursEnd` (nullable; default null = no quiet hours).

**Channel delivery mode** (per alert, per channel):
- `realtime` — one notification per match, immediately.
- `digest-15m` — batch matches, send every 15 minutes.
- `digest-hourly` — batch, hourly.
- `digest-daily` — batch, once a day at user's chosen hour (default 8am local).

A channel-mode matrix is shown in the alert editor: rows = alerts, columns = channels, cell = mode dropdown.

**Channel abstraction**: a backend `INotificationChannel` interface with `Send(NotificationPayload)` and `Verify(destination)`. Only `EmailChannel` and `SlackChannel` are implemented in MVP. SMS, push, webhook are stubbed but compile. Adding a new channel = one new class + DI registration.

### 2.5 My Alerts dashboard

- List of alerts grouped by type (News, Markets, Disasters).
- Per alert: name, filters summary, channel-mode matrix, last fired, toggle on/off, edit, delete.
- Inline **"test this alert"** button: re-runs the matcher with the most recent N events and shows what *would* have fired. Cheap, builds trust, **in MVP** (per resolved decision 2).
- "Activity" tab: a feed of all notifications sent in the last 30 days (resend link, mark as read, view source event).

### 2.6 Settings

- Account (name, email, time zone, password change, account deletion).
- Channels (add/remove email addresses, Slack webhooks; verify state; default per-type).
- Notification preferences (default delivery mode per type, quiet hours, daily digest hour).
- API tokens (roadmap; not in MVP — placeholder tab labeled "coming soon" so the nav structure is final).

### 2.7 Public pages (SEO)

- **Landing page** with hero, "what is this", "how it works", "supported sources", CTA → sign up. Markdown-backed, build-time generated.
- **Pricing/About**: open-source, no paid tiers. **No donate button in MVP** (per resolved decision 4). If we have spare time near the end of the 24h, a "Support this project" footer link to a placeholder page is acceptable; otherwise omit entirely.
- **Privacy / Terms**: legal pages, generated from markdown at build time. Plain English.
- **Status page** (read-only, from `/api/health` + last-poll timestamps per source).

---

## 3. Admin features (MVP)

A separate `/admin` area. Guarded by admin-only permissions (e.g. `Users.Read.Any`, `Sources.Write.Any`). Only seeded users with a row in `UserRoles` linking to the `Admin` role can access (bootstrap admin = first user or env var). RBAC implementation details in `2-stack.md`.

### 3.1 Data source management

- **News sources**: add/edit/remove RSS/Atom feeds. Show last fetch time, last error, fetch count, item count. Manual "fetch now" button.
- **Market symbols**: enable/disable symbols, group into sectors (for the UI to filter by), set market hours (for "during trading hours only" filtering post-MVP).
- **Disaster sources**: enable/disable each upstream (USGS, GDACS, NHC), set default severity thresholds.

Admin-managed only in MVP. Roadmap: hybrid (users can request a source, admin approves → it appears in the public catalog).

**Pros of admin-managed** (what we chose and why):
- One polling job per source = cheap, predictable load.
- Cached, deduped, and rate-limited centrally.
- We can vet quality / legal / security before exposing a feed.
- Bad URLs / spam is not a problem.

**Cons** (so we remember the cost):
- Limited selection. Power users feel constrained.
- More work for the admin. The roadmap hybrid model addresses this.

### 3.2 User management

- Search users by email/display name.
- View: alerts, channels, last login, signup date, roles (via the `UserRoles` join — one user can have multiple roles).
- Actions: change role (insert/delete a `UserRoles` row), suspend (login blocked, alerts paused), restore, hard delete (with 7-day soft-delete window for accidental clicks).
- Audit log of admin actions.

### 3.3 System health / observability

A dashboard with:
- **Source status grid**: per source, last fetch time, error rate over last 1h/24h, queue depth.
- **Delivery status**: emails sent / Slack sent, success rate, average latency, error reasons (top 10).
- **Background jobs**: matcher queue depth, oldest unprocessed event age, worker count.
- **Matcher stats**: alerts evaluated/min, matches/min, false-positive feedback (future).
- **Errors**: top 20 application exceptions in the last 24h (structured logs).

This is a one-page view. We use free observability in MVP — structured logs to stdout + a SQLite table for the rolling counters. (Roadmap: OpenTelemetry → free Grafana Cloud or self-host.)

### 3.4 Manual announcements

- Admin can compose a message → choose target (all users / specific segments / specific users) → choose channels.
- Useful for "we're upgrading X tonight" notices, not for marketing (we are not a marketing product).
- Logged in audit log. Subject to daily rate limit (default 1/week per user — adjustable).

---

## 4. Domain model (entities, MVP)

These are the entities we will create. Naming is a proposal; final names go in `models/`.

- **User**: id, email, password_hash, display_name, time_zone, created_at, status, must_change_password. (The user's role is a row in `UserRoles`; there is no `Role` column on `User`.)
- **EmailVerification** / **PasswordResetToken**: one-shot tokens, expiry, used_at.
- **Channel**: id, user_id, type (`email` | `slack`), destination, verified, quiet_hours_start (nullable, local time), quiet_hours_end (nullable, local time), created_at.
- **Alert**: id, user_id, name, type (`news` | `market` | `disaster`), enabled, filters (typed JSON per alert type), created_at, updated_at.
- **AlertChannelMode**: alert_id, channel_id, mode (`realtime` | `digest-15m` | `digest-hourly` | `digest-daily`).
- **Event**: id, source_id, external_id, type, payload (JSON), occurred_at, fetched_at. (The "raw" events from upstream.)
- **Match**: id, alert_id, event_id, fired_at. (Audit trail of "this event fired this alert".)
- **Notification**: id, user_id, alert_id, match_id, channel_id, mode, status, sent_at, error.
- **Source**: id, type (`news` | `market-symbol` | `disaster`), name, config (JSON), enabled, last_fetched_at, last_error.
- **AuditLog**: id, actor_user_id, action, target_type, target_id, metadata, created_at.

Indexes (SQLite → Postgres-compatible):
- `Event (source_id, occurred_at)` for the matcher window query.
- `Match (alert_id, fired_at)` for "what fired recently" views.
- `Notification (user_id, sent_at desc)` for the activity feed.

<!-- done: 2026-06-04, see PR pending — all 10 entities (plus 3 auth-token entities EmailVerification / PasswordResetToken / RefreshToken, which are part of the Wave 3 contract) are scaffolded in the model. The 3 composite indexes are declared via IEntityTypeConfiguration. FK relationships are declared in every dependent configuration. The InitialSchema migration is generated and applied. Deferred items: see docs/handoffs/wave2-to-future.md. -->

---

## 5. Background work (MVP)

These run as background services in the C# host (background services / `IHostedService`). No external queue.

1. **News poller**: every 2 min, fetch each enabled RSS feed, normalize, dedupe by `external_id`, insert into `Event`.
2. **Disaster poller**: every 5 min, USGS + GDACS + NHC, same dedupe/insert.
3. **Market poller**: every 5 min, call yfinance sidecar for each enabled symbol in watchlists, store latest quote. Re-evaluate market alerts against the rolling window after each poll.
4. **Matcher**: after each poller, find alerts whose filters match new events since the last run. Insert `Match` rows, schedule notifications.
5. **Notification dispatcher**: routes matches to channels, respecting `AlertChannelMode`. Realtime sends immediately — **except** when the channel's quiet hours are active, in which case the notification is queued and re-attempted at the next allowed moment. Digest modes wait for the next tick.
6. **Digest scheduler**: per-(user, mode) tick — pulls matches in the window, builds a digest, sends.
7. **Cleanup**: prune `Event` > 30d, `Notification` > 30d, `AuditLog` > 90d, expired tokens every hour. (Retention is per resolved decision 5.)

---

## 6. Cross-cutting requirements (MVP)

- **Reliability first** (you stated this). The matcher and dispatcher are idempotent (a `Match` is unique per `(alert_id, event_id)`), retried with exponential backoff, and any failure that could affect user-visible state is surfaced to the admin health page within 60s.
- **Privacy by default**: every PII field is documented in the schema, encryption-at-rest is via the disk for SQLite MVP (Postgres in prod), and the Privacy page lists what we store and why.
- **No ads. No tracking. No third-party scripts on the public pages.** If we ever add analytics, it is self-hosted (Plausible self-host or similar) and opt-in.
- **Accessibility**: MUI components + a base level of keyboard nav + color contrast. The admin pages are not the priority; user pages are.
- **i18n-ready copy**: every user-visible string lives in a translation file (even if there's only `en.json` in MVP). Adding a language is a file, not a code change.

---

## 7. Out of MVP (roadmap)

Captured so we don't forget and so the design accounts for them:

- Hybrid data sources (user requests → admin approves).
- More market trigger types (price threshold, volume spike).
- More channels (SMS via Twilio, push, Discord, Teams, generic webhook).
- API tokens + read-only public API for users to build their own integrations.
- Multi-language UI.
- Native mobile apps.
- Federation: users share alert templates (with opt-in public library).
- End-to-end encryption of source payloads at rest.

---

## 8. Resolved decisions (locked in)

1. **Quiet hours** for notifications: **in MVP.** User picks a start/end time per channel (e.g. "no Slack between 22:00 and 07:00 local"). Real-time matches that fall in quiet hours are queued and sent at the next allowed window; digest modes are unaffected. Schema is `Channel.QuietHoursStart`, `Channel.QuietHoursEnd` (nullable). Defaults: off.
2. **Per-alert "test"** button: **in MVP.** Re-runs the matcher against the most recent N events for that alert and shows what *would* have fired. Cheap, builds trust.
3. **Email channel verification**: **confirmation link required** on add. Sending a one-time code to the address; the user pastes it (or clicks the link) to confirm. We don't send real alerts to an unverified address.
4. **Donations later**: **no button in MVP.** If we have spare time near the end of the 24h, a "Support this project" footer link to a placeholder page is acceptable; otherwise omit entirely. Last-priority.
5. **Audit log retention**: **90 days for `AuditLog`; 30 days for `Match` / `Event` / `Notification`** in MVP. Pruning is part of the background cleanup job.

All other items in this doc are pending your review. If you sign off, we move on to `2-stack.md` with the decisions above as constraints.
