---
description: 'Secrets rules. No real secrets in the repo, ever.'
applyTo: '**/.env*,**/appsettings.*.json,**/secrets.*,**/appsettings.Development.json'
---

# Secrets — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md).

## What is a "secret"

Anything that grants access to a system on behalf of the project:

- Database connection strings with credentials
- API keys (SMTP password, Resend key, SendGrid key, Slack webhook URLs)
- JWT signing keys
- OAuth client secrets
- Encryption keys (data-at-rest, password hashing salts)
- Any URL with a username + password in it

## What is NOT a secret

- Public OAuth client IDs (these are public by design)
- Public API base URLs (`https://api.resend.com`)
- Email addresses (PII, but not a secret in the threat model)
- `appsettings.json` non-sensitive defaults (e.g. `Logging:LogLevel:Default: "Information"`)

## Where secrets live

- **Locally (dev)**: `dotnet user-secrets` for the .NET project, `.env` for the yfinance sidecar and any other tools. `.env` is **gitignored**; only `.env.example` is committed.
- **Production**: environment variables on the host. For systemd, `EnvironmentFile=/etc/sonrisa/secrets.env`. For Windows Service, the service's environment block.
- **Never in**: a `*.json` config file in the repo, a `.env` file in the repo, a code constant, a PR description, a CI workflow file.

## Pre-commit / pre-tool checks

- The `pre-commit` Git hook runs a regex-based scan for common secret patterns (AWS keys, GitHub PATs, Resend keys, Slack tokens, JWT secrets, base64-encoded blobs > 200 chars that match `==` or `=` padding).
- The `pre-tool` hook (`.github/hooks/hooks.json`) blocks writes to `.env`, `appsettings.Production.json`, and any `secrets.*` file.
- If the agent sees what looks like a real key — in a log, a config, a screenshot — **stop and tell the user**. Do not commit it, do not echo it, do not include it in an error message.

## If a secret is leaked

1. Tell the user immediately. Do not edit the file and commit the change.
2. The user rotates the secret at the provider.
3. The user updates the env var on the host.
4. The agent writes a follow-up PR to remove the leaked value from history (BFG or `git filter-repo`).

This is a fire drill. The tripwires exist to prevent it. Don't be the one who tests them.

## Forbidden patterns (the agent must not introduce these)

- `.env` in the repo (only `.env.example`)
- Real keys in `appsettings.*.json` (use placeholders: `"Smtp__Password": "REPLACE_ME"`)
- Real keys in a commit message, PR description, or code comment
- Echoing a secret in a log line (even at debug level)
- A `try/catch` that swallows a "config missing" exception (fail fast at startup, not at first request)
- Hard-coded webhook URLs or SMTP passwords in code constants
- A "secret rotation" feature that doesn't actually rotate (no `UpdateSecret` API method without a real implementation)
- Copying a real `appsettings.Development.json` from a coworker without scrubbing
