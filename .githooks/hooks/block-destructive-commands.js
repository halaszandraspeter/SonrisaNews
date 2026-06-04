#!/usr/bin/env node
// .githooks/hooks/block-destructive-commands.js
// Reject terminal commands that could damage the system, push to main, or update a non-dev DB.

const forbidden = [
  {
    pattern: /\brm\s+-rf?\b(?!\s+(node_modules|\.next|bin|obj|dist|\.pytest_cache|__pycache__))/,
    reason: 'rm -rf is only allowed inside safe build/cache directories.',
  },
  { pattern: /git\s+push\s+--force\b.*\b(main|master)\b/, reason: 'Force-push to main is forbidden.' },
  { pattern: /git\s+push\s+-f\b.*\b(main|master)\b/, reason: 'Force-push to main is forbidden.' },
  {
    pattern: /dotnet\s+ef\s+database\s+update\b/,
    reason: 'Run `dotnet ef database update` manually, not from an agent. Use migrations script + review first.',
    // Allow only when explicitly scoped to a dev connection string.
    exception: (cmd) => /Data\s*Source\s*=\s*data\//i.test(cmd) || /Data\s*Source\s*=\s*:memory:/i.test(cmd),
  },
  {
    pattern: /--env\s+production\b/,
    reason: 'Commands that target the production environment are not run by the agent.',
  },
];

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

  for (const rule of forbidden) {
    if (rule.pattern.test(cmd)) {
      if (rule.exception && rule.exception(cmd)) continue;
      process.stderr.write(`❌ BLOCKED command: ${cmd}\n   ${rule.reason}\n`);
      process.exit(2);
    }
  }

  process.exit(0);
});
