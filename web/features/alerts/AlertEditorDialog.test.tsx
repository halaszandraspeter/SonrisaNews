import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { vi, describe, it, expect, beforeEach } from "vitest";
import { AlertEditorDialog } from "./AlertEditorDialog";

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    GET: vi.fn(),
    POST: vi.fn(),
    PUT: vi.fn(),
    DELETE: vi.fn(),
  },
}));

describe("AlertEditorDialog", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
  });

  const renderDialog = (props?: Partial<React.ComponentProps<typeof AlertEditorDialog>>) =>
    render(
      <QueryClientProvider client={queryClient}>
        <AlertEditorDialog
          open
          onClose={vi.fn()}
          onSaved={vi.fn()}
          {...props}
        />
      </QueryClientProvider>,
    );

  it("renders a create-mode dialog with a Name field and a Type picker", () => {
    renderDialog();
    expect(screen.getByRole("heading", { name: /new alert/i })).toBeTruthy();
    // The Name TextField is a <c>textbox</c> with the accessible
    // name "Name". The radio group has its own role and is queried
    // separately.
    expect(screen.getByRole("textbox", { name: /^name$/i })).toBeTruthy();
    // The type picker is rendered as radio buttons.
    expect(screen.getByRole("radio", { name: /^news$/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /^market$/i })).toBeTruthy();
    expect(screen.getByRole("radio", { name: /^disaster$/i })).toBeTruthy();
  });

  it("disables the Save button when the name is empty", () => {
    renderDialog();
    const saveButton = screen.getByRole("button", { name: /save|create/i });
    expect((saveButton as HTMLButtonElement).disabled).toBe(true);
  });

  it("does not render anything when open is false", () => {
    renderDialog({ open: false });
    expect(screen.queryByRole("heading", { name: /new alert/i })).toBeNull();
  });

  it("shows market-specific filter fields when Market is selected", async () => {
    renderDialog();
    const marketRadio = screen.getByRole("radio", { name: /^market$/i });
    fireEvent.click(marketRadio);
    await waitFor(() => {
      expect(screen.getByLabelText(/symbols/i)).toBeTruthy();
    });
  });

  it("shows disaster-specific filter fields when Disaster is selected", async () => {
    renderDialog();
    const disasterRadio = screen.getByRole("radio", { name: /^disaster$/i });
    fireEvent.click(disasterRadio);
    await waitFor(() => {
      expect(screen.getByLabelText(/regions/i)).toBeTruthy();
    });
  });

  it("shows news-specific filter fields when News is selected (default)", () => {
    renderDialog();
    // News is the default type.
    expect(screen.getByLabelText(/keyword/i)).toBeTruthy();
  });

  it("closes the dialog when Cancel is clicked", () => {
    const onClose = vi.fn();
    renderDialog({ onClose });
    screen.getByRole("button", { name: /cancel/i }).click();
    expect(onClose).toHaveBeenCalled();
  });
});
