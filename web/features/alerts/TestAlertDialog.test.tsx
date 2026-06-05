import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { vi, describe, it, expect, beforeEach } from "vitest";

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    GET: vi.fn(),
    POST: vi.fn(),
    PUT: vi.fn(),
    DELETE: vi.fn(),
  },
}));

import { TestAlertDialog } from "./TestAlertDialog";
import { apiClient } from "@/lib/api/client";

const ALERT_ID = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";

describe("TestAlertDialog", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    // Default mock: empty-list response. Tests that need a
    // different shape override with mockResolvedValueOnce.
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [],
      response: { ok: true, status: 200 },
    });
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
  });

  const renderDialog = (
    props?: Partial<React.ComponentProps<typeof TestAlertDialog>>,
  ) =>
    render(
      <QueryClientProvider client={queryClient}>
        <TestAlertDialog
          open
          onClose={vi.fn()}
          alertId={ALERT_ID}
          alertName="My alert"
          {...props}
        />
      </QueryClientProvider>,
    );

  it("renders the alert name in the dialog title", async () => {
    renderDialog();
    expect(
      screen.getByRole("heading", { name: /test "my alert"/i }),
    ).toBeTruthy();
    // The dialog fires the matcher on open. Wait for the
    // post-mutation state to settle so the test's effect
    // resolution doesn't fire an unwrapped act() update.
    await waitFor(() => {
      expect(screen.getByText(/no events in the last 50/i)).toBeTruthy();
    });
  });

  it("shows a running state while the matcher query is in flight", async () => {
    // Never-resolving POST so the mutation stays pending while
    // the test reads the DOM. The dialog fires the request in
    // a useEffect on open=true, so the first render shows
    // nothing in the body and the spinner lands after the
    // effect runs.
    (apiClient.POST as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {}),
    );

    renderDialog();

    await waitFor(() => {
      expect(screen.getByRole("progressbar")).toBeTruthy();
    });
    expect(screen.getByText(/running the matcher/i)).toBeTruthy();
  });

  it("shows an empty-state message when the matcher returns zero hits", async () => {
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: [],
      response: { ok: true, status: 200 },
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText(/no events in the last 50/i)).toBeTruthy();
    });
  });

  it("renders a list of summaries when the matcher returns hits", async () => {
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: [
        { EventId: "11111111-1111-4111-8111-111111111111", Summary: "Breaking: …" },
        { EventId: "22222222-2222-4222-8222-222222222222", Summary: "Markets: …" },
      ],
      response: { ok: true, status: 200 },
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText(/2 events would have fired this alert/i)).toBeTruthy();
    });
    expect(screen.getByText("Breaking: …")).toBeTruthy();
    expect(screen.getByText("Markets: …")).toBeTruthy();
  });

  it("surfaces empty-payload hits as a distinct 'empty payload' chip", async () => {
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: [
        { EventId: "11111111-1111-4111-8111-111111111111", Summary: "" },
      ],
      response: { ok: true, status: 200 },
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText(/1 event would have fired this alert/i)).toBeTruthy();
    });
    // The empty-payload chip is the user-visible signal that
    // the matcher's Summarize() returned string.Empty
    // (no title / summary / description in the event's
    // payload). This is a different signal from "no match".
    expect(screen.getByText("empty payload")).toBeTruthy();
  });

  it("uses singular grammar when the matcher returns exactly one hit", async () => {
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: [
        { EventId: "11111111-1111-4111-8111-111111111111", Summary: "Only one" },
      ],
      response: { ok: true, status: 200 },
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText(/1 event would have fired this alert/i)).toBeTruthy();
    });
    // No plural "events" before the second hit lands.
    expect(screen.queryByText(/1 events/i)).toBeNull();
  });

  it("renders an error message when the matcher call fails", async () => {
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: undefined,
      response: { ok: false, status: 500 },
      error: { message: "Server exploded" },
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeTruthy();
    });
    expect(screen.getByText(/server exploded/i)).toBeTruthy();
  });

  it("calls onClose when the Close button is clicked", async () => {
    // A pending mutation keeps the dialog from closing via
    // backdrop click but the explicit Close button is always
    // enabled. Use a never-resolving POST so the mutation
    // doesn't flip to idle and unmount.
    (apiClient.POST as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {}),
    );

    const onClose = vi.fn();
    renderDialog({ onClose });
    fireEvent.click(screen.getByRole("button", { name: /^close$/i }));
    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("re-runs the matcher when the Run again button is clicked", async () => {
    (apiClient.POST as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [],
      response: { ok: true, status: 200 },
    });

    renderDialog();

    // First call: open → run.
    await waitFor(() => {
      expect(apiClient.POST).toHaveBeenCalledTimes(1);
    });

    // Wait for the first call to resolve before clicking
    // "Run again" — the new async handler awaits the prior
    // promise (mutateAsync) before firing the next. The
    // result of the first call is in the DOM as the empty
    // state, which is the signal that the mutation is
    // settled and a second click is safe.
    await waitFor(() => {
      expect(screen.getByText(/no events in the last 50/i)).toBeTruthy();
    });

    // Click "Run again" — fires a second call.
    fireEvent.click(screen.getByRole("button", { name: /run again/i }));

    await waitFor(() => {
      expect(apiClient.POST).toHaveBeenCalledTimes(2);
    });
  });

  it("ignores a Run-again click while the previous matcher run is in flight", async () => {
    // A never-resolving first call. The race fix has two
    // layers:
    //   1. UX: the "Run again" button is `disabled` while
    //      the mutation is in flight (asserted below via
    //      the DOM `disabled` property — Vitest has no
    //      `toBeDisabled` matcher; that's a Jest matcher).
    //   2. Data: the `runAgain` handler short-circuits on
    //      `mutation.isPending` even if a click somehow
    //      bypasses the disabled prop (asserted via the
    //      call count).
    // Both layers are tested; if either weakens, the
    // corresponding assertion fails.
    (apiClient.POST as ReturnType<typeof vi.fn>).mockReturnValue(
      new Promise(() => {}),
    );

    renderDialog();

    // First call: open → run, in flight forever.
    await waitFor(() => {
      expect(apiClient.POST).toHaveBeenCalledTimes(1);
    });

    // UX-layer defense: the button is disabled while the
    // mutation is pending. A future refactor that drops
    // the `disabled` prop would weaken the UX without
    // breaking the handler; this assertion catches that.
    const runAgainButton = screen.getByRole("button", { name: /run again/i });
    expect((runAgainButton as HTMLButtonElement).disabled).toBe(true);

    // Data-layer defense: force a click that real users
    // couldn't perform. `userEvent.click` respects the
    // `disabled` prop and would no-op; `fireEvent.click`
    // dispatches the synthetic event regardless. The
    // handler's `if (mutation.isPending) return;`
    // short-circuits the second request.
    fireEvent.click(runAgainButton);

    // Yield once to the macrotask queue so the synchronous
    // handler runs before we assert. (The handler is fully
    // sync on the success path: guard → reset → mutateAsync
    // returns a promise that never resolves. The setTimeout
    // is one tick; no real time elapses.)
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(apiClient.POST).toHaveBeenCalledTimes(1);
  });

  it("does not render the dialog content when open is false", () => {
    renderDialog({ open: false });
    expect(screen.queryByRole("heading", { name: /test "my alert"/i })).toBeNull();
  });
});
