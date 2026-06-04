---
description: 'EF Core migration rules. Migrations are forward-only and CLI-owned.'
applyTo: 'backend/**/Migrations/**'
---

# EF Core Migrations — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §3.

## Ownership

- **Migrations are owned by the EF Core CLI.** The only way to create or modify a migration is:
  ```bash
  dotnet ef migrations add <Name> --project src/SonrisaNews.Infrastructure
  ```
- **Never hand-edit a migration file** once it's been committed. A `pre-tool` hook blocks edits to `backend/**/Migrations/*.cs`. If you need to change a migration, add a new one.

## Structure of a migration

Every migration:

- Has an `Up` method with a `// Forward-only after merge.` comment as the first line.
- Has a `Down` method (for reference; we don't run `Down` in prod, but it must compile).
- Uses the migration builder API (`migrationBuilder.CreateTable(...)`, `migrationBuilder.AddColumn(...)`, etc.), not raw SQL, unless absolutely necessary.
- If raw SQL is necessary, the `Up` includes the equivalent DDL and the `Down` includes the reverse DDL.

## Lifecycle

1. **In development**: run `dotnet ef migrations add <Name>` after changing an entity. Commit the new files: `YYYYMMDDHHMMSS_<Name>.cs` and the updated `SonrisaNewsModelSnapshot.cs`.
2. **In review**: the reviewer checks the migration diff for accidental drops, dangerous defaults, and missing indexes.
3. **In CI**: a separate `migrations-build` job runs `dotnet ef migrations script --no-build --output migrations.sql` and uploads the SQL artifact for visibility. It does **not** apply.
4. **In prod**: deployment runs `dotnet ef database update` as a pre-start step. Forward-only. No rollback via `Down` — that requires a new migration.

## What triggers a migration

Any of:
- Adding / removing / renaming an entity property.
- Adding / removing / renaming a relationship.
- Adding / removing an index.
- Changing a column type or nullability.
- Renaming a table or column.

Adding a new entity does **not** require a separate `CreateTable` migration — the `add` command picks it up from the model.

## Indexes

- **Every foreign key gets an index** by default. EF Core does this automatically; verify in the snapshot.
- **Query-hot columns** (e.g. `Event.OccurredAt`, `Match.FiredAt`, `Notification.SentAt`) get an index. Add it via `[Index(nameof(...))]` on the entity.
- **Composite indexes** for the matcher window query: `Event (SourceId, OccurredAt)`. EF generates this from `modelBuilder.Entity<Event>().HasIndex(...)`.

## Forward-only in practice

- **Don't rename a column in a migration.** Add the new column, backfill it, deploy, then add a *second* migration to drop the old column. Two migrations are better than one breaking deploy.
- **Don't change a column type** without a `// TODO: backfill` comment in the migration and a follow-up migration to remove the backfill code.
- **Don't drop a table** that has data without a soft-delete intermediate step.

## Forbidden patterns (the agent must not introduce these)

- Hand-edited `**/Migrations/*.cs` after merge
- `migrationBuilder.Sql(...)` with concatenated strings (always parameterized)
- `migrationBuilder.DropColumn(...)` without a follow-up "did you mean to?" review note
- `database.Migrate()` in production startup code (use `dotnet ef database update` as a pre-start step)
- Custom `IMigration` implementations (the scaffolder's output is the only migration type we use)
- Direct edits to `SonrisaNewsModelSnapshot.cs` (the scaffolder owns it)
