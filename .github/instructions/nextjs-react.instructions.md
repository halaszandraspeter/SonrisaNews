---
description: 'Next.js / React / TypeScript coding standards for Sonrisa News.'
applyTo: 'web/**/*.{ts,tsx}'
---

# Next.js / React — Sonrisa News

> **Read first**: [AGENTS.md](../../../AGENTS.md), [docs/roadmap/2-stack.md](../../../docs/roadmap/2-stack.md) §2.

## Stack

- Next.js 16 App Router. React 19. TypeScript **strict**. MUI v9.
- TanStack Query v5 for client state. React Hook Form + Zod for forms.
- No Tailwind. No shadcn. No Chakra. **MUI only.**

## Server vs Client components

- **Default to Server Components.** Add `'use client'` only when the component needs state, effects, event handlers, browser APIs, or third-party client-only libraries.
- **Push `'use client'` to the leaf.** A wrapper with state, a `<form>` that uses `useForm`, a button with `(onClick)`. Don't mark a whole page client-side just because one child needs it.
- **Never import server-only modules from a client component.** `fs`, `path`, `next/headers`, `next/cookies`, server-only secrets, etc. are off-limits on the client. Use the `server-only` package to make this a build error.
- **Data fetching**: Server Components fetch directly. Client components use `useQuery`. Don't mix the two (Server Component fetches, then passes data to a Client Component via props).

## MUI conventions

- **`sx` prop for dynamic styles.** `style={{}}` is forbidden when the value is computed at runtime.
- **Theme tokens, not hard-coded values.** `theme.spacing(2)`, `theme.palette.primary.main`, never `'#1976d2'` or `'16px'` in component code.
- **One `<ThemeProvider>` at the root** of `(app)`, `(admin)`, and `(auth)` route groups. Don't nest.
- **No inline `style={{}}` with static values** — use `sx` consistently.
- **`useTheme()` is fine**; reading `theme` from context in the same component is preferred over passing it down.
- **Iconify or `@mui/icons-material`** for icons. Don't import random SVG files.

## Forms

- React Hook Form + Zod schema, colocated. Schema lives next to the form, e.g. `SignUpForm.tsx` + `signUpSchema.ts`.
- **One Zod schema per form.** Reuse via `z.infer<typeof schema>` for the form's input/output types.
- **Server errors come back typed.** Map the API's `ProblemDetails` (RFC 7807) to a Zod issue, display with `<FormHelperText error>`.
- **No `defaultValues` from props without memoization.** Pass them through `useMemo` or use `values` from a parent query.

## React Query

- **Query keys are tuples, hierarchical**: `['alerts', alertId, 'history']`. Invalidation: `queryClient.invalidateQueries({ queryKey: ['alerts'] })`.
- **No `refetchOnWindowFocus` globally off** unless the data is large and stale is fine. Default is on.
- **Mutations invalidate the smallest set of queries possible.** Don't blast `['alerts']` if you only need `['alerts', alertId]`.
- **Loading states are explicit.** `if (isPending) return <Skeleton />`. No spinners unless the operation is user-initiated.
- **Error states are explicit.** Render `<Alert severity="error">` with a retry button. Never `console.error` and render `null`.

## Auth client

- Access token in memory (Zustand or React context). Refresh token in an httpOnly cookie (set by the backend).
- **`fetch` wrapper intercepts 401 once**, calls `/auth/refresh`, retries the original request. If refresh fails, redirect to sign-in.
- **Never store tokens in `localStorage` or `sessionStorage`.**

## OpenAPI client

- `pnpm --dir web generate:api` regenerates types from the backend's Swagger doc. Run this on every backend change.
- **The client is committed.** Drift is a build error in CI (a separate job compares the committed client to a fresh regeneration and fails if they differ).
- **Use `openapi-fetch` for requests, not raw `fetch`.** Types come for free.

## Style

- **Named exports only.** No `export default`. Components, hooks, utilities — all named.
- **Functional components only.** No class components.
- **No `useEffect` for data fetching.** Use `useQuery`. `useEffect` is for side effects on prop changes (rare).
- **No `useMemo` / `useCallback` "just in case".** Only when there's a measured or obvious reason.
- **No `any`.** `unknown` if you must, then narrow.
- **No barrel files** (`index.ts` re-exports). Direct imports: `import { Foo } from '@/features/alerts/Foo'`.
- **File size**: ~300 lines max. Extract when approaching the limit.

## Tests

- Vitest + Testing Library for unit. Playwright for e2e.
- **Test behavior, not implementation.** Don't assert internal state; assert the rendered output or the API call.
- **No real network in unit tests.** Use `msw` to mock the OpenAPI client.
- **No real time** without a controllable clock. Use `vi.useFakeTimers()`.

## Accessibility (a11y)

- Every form input has a `<label>` (use MUI's `<TextField label="…">`).
- Every icon-only button has an `aria-label`.
- Every interactive element is keyboard-reachable in document order.
- Color contrast meets WCAG AA (the default MUI theme does).
- **No `div` as a button.** Use `<button>` or `<Button>`.

## Forbidden patterns (the agent must not introduce these)

- `localStorage` / `sessionStorage` for tokens
- Inline `style={{}}` with dynamic values
- `'use client'` at the page level
- `any` in TypeScript
- `export default` for components
- `useEffect` to fetch data
- `console.log` in production code (use a logger if you really need one)
- Hard-coded URLs (`'http://localhost:5080'`) — read from `process.env.NEXT_PUBLIC_API_URL`
- Importing from `index.ts` barrel files
- `// @ts-ignore` (use `// @ts-expect-error` with a reason, or fix the type)
- Tailwind, shadcn, Chakra, or any UI library other than MUI
