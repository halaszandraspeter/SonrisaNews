import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { vi, describe, it, expect, beforeEach } from "vitest";
import { AlertsEditorHost, useAlertEditorHost } from "./AlertsEditorHost";
import { NewAlertButton } from "./NewAlertButton";

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    GET: vi.fn(),
    POST: vi.fn(),
    PUT: vi.fn(),
    DELETE: vi.fn(),
  },
}));

// A test leaf that lets the test trigger openForCreate /
// openForEdit / close directly, so we can pin the host's
// contract without going through the dialog's mutation
// lifecycle.
function TestLeaf() {
  const host = useAlertEditorHost();
  return (
    <div>
      <button data-testid="open-create" onClick={host.openForCreate}>
        create
      </button>
      <button
        data-testid="open-edit"
        onClick={() =>
          host.openForEdit({
            Id: "alert-1",
            Name: "Test",
            Type: "News",
            Enabled: true,
            Filters: "{}",
            CreatedAt: "2026-06-05T00:00:00Z",
            UpdatedAt: "2026-06-05T00:00:00Z",
          })
        }
      >
        edit
      </button>
      <button data-testid="close" onClick={host.close}>
        close
      </button>
      <span data-testid="state-mode">{host.state.mode}</span>
    </div>
  );
}

describe("AlertsEditorHost", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
  });

  it("starts in closed state", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AlertsEditorHost>
          <TestLeaf />
        </AlertsEditorHost>
      </QueryClientProvider>,
    );
    expect(screen.getByTestId("state-mode").textContent).toBe("closed");
  });

  it("transitions to create when openForCreate is called", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AlertsEditorHost>
          <TestLeaf />
        </AlertsEditorHost>
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByTestId("open-create"));
    expect(screen.getByTestId("state-mode").textContent).toBe("create");
  });

  it("transitions to edit when openForEdit is called", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AlertsEditorHost>
          <TestLeaf />
        </AlertsEditorHost>
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByTestId("open-edit"));
    expect(screen.getByTestId("state-mode").textContent).toBe("edit");
  });

  it("returns to closed when close is called", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AlertsEditorHost>
          <TestLeaf />
        </AlertsEditorHost>
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByTestId("open-create"));
    fireEvent.click(screen.getByTestId("close"));
    expect(screen.getByTestId("state-mode").textContent).toBe("closed");
  });

  it("throws when useAlertEditorHost is used outside the host", () => {
    // Suppress the React error boundary noise — the test
    // only cares that the hook throws.
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<TestLeaf />)).toThrow(/AlertsEditorHost/);
    consoleError.mockRestore();
  });

  it("renders the NewAlertButton which calls openForCreate on click", () => {
    render(
      <QueryClientProvider client={queryClient}>
        <AlertsEditorHost>
          <NewAlertButton />
          <TestLeaf />
        </AlertsEditorHost>
      </QueryClientProvider>,
    );
    // The button starts the dialog in closed state.
    expect(screen.getByTestId("state-mode").textContent).toBe("closed");
    // Clicking "New alert" transitions the host to create mode.
    fireEvent.click(screen.getByRole("button", { name: /new alert/i }));
    expect(screen.getByTestId("state-mode").textContent).toBe("create");
  });
});
