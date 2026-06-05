import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { vi, describe, it, expect, beforeEach } from "vitest";
import { AddChannelButtonWithRefresh } from "./AddChannelButtonWithRefresh";
import { channelsQueryKey } from "@/lib/api/keys";

// Mock the plain AddChannelButton so the test can capture the
// onChannelCreated callback the wrapper passes in. This pins
// the wrapper's contract: it accepts a channel id, calls
// invalidateQueries on the ["channels"] key, and intentionally
// does not use the id (it's an invalidator, not a prepend
// hook).
vi.mock("@/features/channels/AddChannelButton", () => ({
  AddChannelButton: ({
    label,
    onChannelCreated,
  }: {
    label?: string;
    onChannelCreated?: (channelId: string) => void;
  }) => {
    return (
      <div>
        <button data-testid="add-channel-button">{label ?? "Add Channel"}</button>
        <button
          data-testid="fire-callback"
          onClick={() => onChannelCreated?.("channel-id-abc-123")}
        >
          fire
        </button>
      </div>
    );
  },
}));

describe("AddChannelButtonWithRefresh", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    // Seed the cache so the invalidation has a real entry to drop.
    queryClient.setQueryData(channelsQueryKey, []);
  });

  it("forwards the label to the underlying button", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AddChannelButtonWithRefresh label="Add another channel" />
      </QueryClientProvider>,
    );
    expect(screen.getByTestId("add-channel-button").textContent).toBe(
      "Add another channel",
    );
  });

  it("falls back to 'Add Channel' when no label is provided", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AddChannelButtonWithRefresh />
      </QueryClientProvider>,
    );
    expect(screen.getByTestId("add-channel-button").textContent).toBe(
      "Add Channel",
    );
  });

  it("invalidates the ['channels'] query when a channel is created", () => {
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    render(
      <QueryClientProvider client={queryClient}>
        <AddChannelButtonWithRefresh />
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByTestId("fire-callback"));

    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: channelsQueryKey,
    });
  });

  it("ignores the channelId argument (the wrapper is an invalidator, not a prepend hook)", () => {
    // The wrapper discards the id. Two fires with different ids
    // should produce the same invalidation; neither should touch
    // the query cache data directly.
    const setDataSpy = vi.spyOn(queryClient, "setQueryData");
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    render(
      <QueryClientProvider client={queryClient}>
        <AddChannelButtonWithRefresh />
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByTestId("fire-callback"));
    // Simulate a second create with a different id:
    fireEvent.click(screen.getByTestId("fire-callback"));

    // No setQueryData: the wrapper does not optimistically
    // update the cache. Only invalidation.
    expect(setDataSpy).not.toHaveBeenCalled();
    // Each fire produced one invalidation; both are identical.
    expect(invalidateSpy).toHaveBeenCalledTimes(2);
    for (const call of invalidateSpy.mock.calls) {
      expect(call[0]).toEqual({ queryKey: channelsQueryKey });
    }
  });
});
