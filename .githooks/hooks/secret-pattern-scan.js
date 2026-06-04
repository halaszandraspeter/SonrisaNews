#!/usr/bin/env node
// .githooks/hooks/secret-pattern-scan.js
// Scan new/edited file content for common secret patterns.
// Allow explicit test fixtures by checking for a 'TEST-ONLY' marker on the same line.

const secretPatterns = [
  { name: 'AWS access key', regex: /AKIA[0-9A-Z]{16}/ },
  { name: 'GitHub personal access token', regex: /ghp_[0-9a-zA-Z]{36}/ },
  { name: 'Resend API key', regex: /re_[0-9a-zA-Z]{20,}/ },
  { name: 'Slack token', regex: /xox[baprs]-[0-9a-zA-Z-]{10,}/ },
  { name: 'PEM private key', regex: /-----BEGIN [A-Z ]*PRIVATE KEY-----/ },
  { name: 'Stripe live key', regex: /sk_live_[0-9a-zA-Z]{24,}/ },
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

  const content =
    (payload.content || '').toString() +
    '\n' +
    ((payload.newText || payload.new_str || '').toString());

  if (!content.trim()) {
    process.exit(0);
  }

  for (const { name, regex } of secretPatterns) {
    const match = content.match(regex);
    if (!match) continue;

    // Allow if the same line has a 'TEST-ONLY' or 'FAKE' marker — the developer
    // is intentionally using a fake value. This is a heuristic, not a guarantee.
    const lineIndex = content.slice(0, match.index).split('\n').length;
    const line = content.split('\n')[lineIndex - 1] || '';
    if (/TEST-ONLY|FAKE|DUMMY|EXAMPLE/i.test(line)) continue;

    process.stderr.write(
      `❌ BLOCKED: possible ${name} in new/edited content.\n` +
        `   Line ${lineIndex}: ${line.trim().slice(0, 80)}…\n` +
        `   If this is a test fixture, add a 'TEST-ONLY' comment on the same line.\n`
    );
    process.exit(2);
  }

  process.exit(0);
});
