#!/usr/bin/env node
// .githooks/hooks/block-forbidden-files.js
// Reject writes to files that must not be edited by the agent.

const path = require('path');

const forbidden = [
  { pattern: /(^|\/)\.env$/, reason: 'Real secrets must not be committed. Edit .env.example only.' },
  { pattern: /(^|\/)\.env\.local$/, reason: 'Local env files are not committed.' },
  { pattern: /appsettings\.Production\.json$/, reason: 'Production config is owned by the user. Propose the change in the PR description.' },
  { pattern: /(^|\/)rbac_policy\.csv$/, reason: 'RBAC policy changes go through a labeled PR (feat(rbac): …).' },
  { pattern: /backend\/.*\/Migrations\/.*\.cs$/, reason: 'Migrations are owned by `dotnet ef migrations add`. Hand-edits blocked.' },
];

function isForbidden(filePath) {
  if (!filePath) return null;
  const normalized = filePath.replace(/\\/g, '/');
  for (const rule of forbidden) {
    if (rule.pattern.test(normalized)) return rule.reason;
  }
  return null;
}

let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => { input += chunk; });
process.stdin.on('end', () => {
  let payload = {};
  try {
    payload = JSON.parse(input);
  } catch {
    // No payload — allow.
    process.exit(0);
  }

  const candidates = [
    payload.path,
    payload.filePath,
    payload.file,
    payload.arguments && payload.arguments.path,
  ].filter(Boolean);

  for (const candidate of candidates) {
    const reason = isForbidden(candidate);
    if (reason) {
      process.stderr.write(`❌ BLOCKED: ${candidate} — ${reason}\n`);
      process.exit(2);
    }
  }

  process.exit(0);
});
