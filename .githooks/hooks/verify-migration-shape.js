#!/usr/bin/env node
// .githooks/hooks/verify-migration-shape.js
// After a 'dotnet ef migrations add' command, verify the generated migration
// has both Up and Down methods. This is a soft check: warn, don't block.

const fs = require('fs');
const path = require('path');

let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => { input += chunk; });
process.stdin.on('end', () => {
  let payload = {};
  try {
    payload = JSON.parse(input);
  } catch {
    process.exit(0);
  }

  const cmd = (payload.command || '').toString();
  if (!/\bdotnet\s+ef\s+migrations\s+add\b/.test(cmd)) {
    process.exit(0);
  }

  // Find the latest .cs file in the Migrations directory.
  const migrationsDir = path.resolve(
    process.cwd(),
    'backend/src/SonrisaNews.Infrastructure/Migrations'
  );

  if (!fs.existsSync(migrationsDir)) {
    process.stderr.write(`⚠️  Migrations directory not found: ${migrationsDir}\n`);
    process.exit(0);
  }

  const files = fs
    .readdirSync(migrationsDir)
    .filter((f) => /^\d{14}_.*\.cs$/.test(f))
    .sort()
    .reverse();

  if (files.length === 0) {
    process.stderr.write('⚠️  No migration files found.\n');
    process.exit(0);
  }

  const latest = path.join(migrationsDir, files[0]);
  const content = fs.readFileSync(latest, 'utf8');

  const hasUp = /protected\s+override\s+void\s+Up\s*\(/.test(content);
  const hasDown = /protected\s+override\s+void\s+Down\s*\(/.test(content);
  const hasForwardOnly = /\/\/\s*Forward-only after merge\./.test(content);

  const issues = [];
  if (!hasUp) issues.push('missing `Up` method');
  if (!hasDown) issues.push('missing `Down` method');
  if (!hasForwardOnly) issues.push('missing `// Forward-only after merge.` comment in `Up`');

  if (issues.length > 0) {
    process.stderr.write(`⚠️  Migration ${files[0]} has issues: ${issues.join(', ')}.\n`);
    process.exit(0); // Soft warning — don't block; the TDD agent will fix it.
  }

  process.stderr.write(`✅ Migration ${files[0]} looks correct.\n`);
  process.exit(0);
});
