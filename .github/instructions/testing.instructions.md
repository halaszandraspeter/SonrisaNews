---
description: 'Testing rules. Hermetic, fast, deterministic, free of live network and real time.'
applyTo: '**/*Tests*/**/*.{cs,ts,tsx,py},**/*.test.{ts,tsx},**/*.spec.{ts,tsx}'
---

# Testing — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §10.

## Principles

- **Hermetic.** No live network. No real SMTP. No real Slack. No real database server (use SQLite in-memory or EF Core's in-memory provider).
- **Deterministic.** No real time. No real randomness without a fixed seed. No concurrency that depends on the scheduler.
- **Fast.** A unit test takes < 50ms. A full unit-test suite takes < 30s. An integration test suite takes < 2 minutes. E2E is the only place we tolerate longer.
- **Isolated.** Each test gets a fresh fixture. No shared mutable state between tests.
- **Behavior-driven.** Test what the code does for the user, not how it's implemented. Don't assert internal state.

## C# (xUnit + FluentAssertions)

- `[Fact]` for single-case tests, `[Theory]` with `[InlineData]` for parameterized.
- Test class name: `<ClassUnderTest>Tests`.
- Test method name: `MethodName_StateUnderTest_ExpectedBehavior` (e.g. `SignUp_DuplicateEmail_Returns409`).
- **Arrange / Act / Assert** with a blank line between sections. `Assert` is a verification, not a sentence — no "expected X but got Y" prefix, FluentAssertions writes that.
- **Use `IClassFixture<T>`** for shared setup that's expensive (e.g. building a `WebApplicationFactory`). Use `IDisposable` for per-test setup.
- **Fakes, not mocks** for repositories and services. A `FakeAlertRepository` is clearer than a `Mock<IAlertRepository>()`.
- **`Moq` is allowed** for things that are awkward to fake (e.g. `IHttpClientFactory`, `ILogger<T>`). Use sparingly.

## TypeScript (Vitest + Testing Library)

- `describe` / `it` / `expect`. `it.each` for parameterized cases.
- File name: `Foo.test.ts(x)` next to the file it tests, or in `__tests__/`.
- **Use `msw`** to mock the OpenAPI client. Don't mock `fetch` directly.
- **`renderHook`** for hook tests. **`render`** for component tests with `screen.getByRole`, `getByLabelText`, etc. — never `getByTestId` unless you must.
- **No snapshot tests** for components (they're brittle and don't catch real bugs). Snapshot tests for serializers (e.g. Zod schema output) are OK.
- **`vi.useFakeTimers()`** when testing time-dependent code. Always `vi.useRealTimers()` in `afterEach`.

## Python (pytest)

- Test file name: `test_foo.py` next to the module it tests.
- Test function name: `test_method_state_expected`.
- **`pytest-asyncio`** for async tests. Mark with `@pytest.mark.asyncio`.
- **No live network.** `yfinance` is mocked at the import boundary. `httpx` is mocked with `respx`.
- **No real time.** Inject a `Clock` dependency.

## E2E (Playwright)

- One Playwright project per audience: `(app)` (user flows), `(admin)` (admin flows).
- Tests are in `web/e2e/`. They use the live dev server (`pnpm dev` must be running, or Playwright starts it via `webServer` config).
- **No mocking in E2E.** The point is to verify the whole stack works.
- **MailHog for email assertions** — read the message via MailHog's HTTP API.
- **No `page.waitForTimeout(2000)`** — use `expect(locator).toBeVisible({ timeout: 5000 })`.
- **Stable selectors first**: `getByRole`, `getByLabel`, `getByText`. `data-testid` only as a last resort.

## Coverage targets (MVP)

- **Unit tests**: 70% on `Domain/`, 70% on `Channels/`, 70% on `Matcher/`, 70% on `Sources/`.
- **Integration tests**: every controller has a happy-path test + one error-path test.
- **E2E**: the one critical user flow (sign-up → create alert → receive email) plus the admin sign-in flow.
- **Coverage is not a goal in itself.** Aim for the targets but don't write tests that don't catch real bugs.

## Forbidden patterns (the agent must not introduce these)

- Live network in unit/integration tests (no `await fetch('https://...')` in tests, no live SMTP, no live Slack)
- Real time in tests (`DateTime.UtcNow`, `time.sleep`, `setTimeout` for non-trivial waits) — use the injected clock
- `Thread.Sleep` in tests (use polling with a timeout)
- `it.skip`, `[Fact(Skip = "...")]` (fix the test or delete it)
- `expect(...).toBeUndefined()` (assert what the value should be, not what it shouldn't)
- `Mock<IAlertRepository>().Verify(...)` for behavior that a `FakeAlertRepository` would express more clearly
- `console.log` in tests (the test framework captures output)
- A test that depends on the previous test's state
- A test that depends on a specific test execution order
- An E2E test that doesn't clean up after itself (creates a user, doesn't delete)
- A flaky E2E test (no `waitForTimeout`, no `try/catch` for "expected" failures)
