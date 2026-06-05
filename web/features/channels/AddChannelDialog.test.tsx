import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { vi, describe, it, expect, beforeEach } from "vitest";
import { AddChannelDialog } from "./AddChannelDialog";

// Mock the API client so future tests that trigger mutations don't
// hit the real openapi-fetch middleware (auth refresh, body
// snapshotting, etc.) inside jsdom.
vi.mock("@/lib/api/client", () => ({
  apiClient: {
    POST: vi.fn(),
  },
}));

const mockOnSuccess = vi.fn();
const mockOnClose = vi.fn();

describe("AddChannelDialog", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    mockOnSuccess.mockClear();
    mockOnClose.mockClear();
  });

  const render_ = (props?: Partial<React.ComponentProps<typeof AddChannelDialog>>) =>
    render(
      <QueryClientProvider client={queryClient}>
        <AddChannelDialog
          open={true}
          onClose={mockOnClose}
          onChannelCreated={mockOnSuccess}
          {...props}
        />
      </QueryClientProvider>
    );

  it("renders the dialog with channel type selection", () => {
    render_();
    const heading = screen.getByRole("heading", { name: /add.*channel/i });
    expect(heading).toBeTruthy();
    expect(screen.getByText(/email/i)).toBeTruthy();
    expect(screen.getByText(/slack/i)).toBeTruthy();
  });

  it("does not render when open is false", () => {
    render_({ open: false });
    const heading = screen.queryByRole("heading", { name: /add.*channel/i });
    expect(heading).toBeNull();
  });

  it("closes the dialog when cancel button is clicked", () => {
    render_();
    const cancelButton = screen.getByRole("button", { name: /cancel/i });
    fireEvent.click(cancelButton);
    expect(mockOnClose).toHaveBeenCalled();
  });

  it("closes the dialog when ESC key is pressed", () => {
    render_();
    const dialog = screen.getByRole("dialog");
    fireEvent.keyDown(dialog, { key: "Escape" });
    expect(mockOnClose).toHaveBeenCalled();
  });

  it("allows user to select email channel and enter destination", () => {
    render_();

    const emailButton = screen.getByRole("button", { name: /email/i });
    fireEvent.click(emailButton);

    const emailInput = screen.getByPlaceholderText(/enter.*email/i) as HTMLInputElement;
    fireEvent.change(emailInput, { target: { value: "test@example.com" } });

    expect(emailInput.value).toBe("test@example.com");
  });

  it("allows user to select slack channel and enter webhook URL", () => {
    render_();

    const slackButton = screen.getByRole("button", { name: /slack/i });
    fireEvent.click(slackButton);

    const urlInput = screen.getByPlaceholderText(/webhook.*url/i) as HTMLInputElement;
    fireEvent.change(urlInput, {
      target: { value: "https://hooks.slack.com/services/T00/B00/XX" },
    });

    expect(urlInput.value).toBe("https://hooks.slack.com/services/T00/B00/XX");
  });

  it("validates email format before submission", async () => {
    render_();

    const emailButton = screen.getByRole("button", { name: /^email$/i });
    fireEvent.click(emailButton);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/enter.*email/i)).toBeTruthy();
    });

    const emailInput = screen.getByPlaceholderText(/enter.*email/i) as HTMLInputElement;
    fireEvent.change(emailInput, { target: { value: "invalid-email" } });

    // Wait for the destination state to be set
    await waitFor(() => {
      expect(emailInput.value).toBe("invalid-email");
    });

    const submitButton = screen.getByRole("button", { name: /add channel/i });
    fireEvent.click(submitButton);

    await waitFor(
      () => {
        const errorText = screen.queryByText(/invalid email/i);
        expect(errorText).toBeTruthy();
      },
      { timeout: 3000 }
    );
    expect(mockOnSuccess).not.toHaveBeenCalled();
  });

  it("validates slack URL format before submission", async () => {
    render_();

    const slackButton = screen.getByRole("button", { name: /^slack$/i });
    fireEvent.click(slackButton);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/webhook.*url/i)).toBeTruthy();
    });

    const urlInput = screen.getByPlaceholderText(/webhook.*url/i) as HTMLInputElement;
    fireEvent.change(urlInput, { target: { value: "not-a-url" } });

    await waitFor(() => {
      expect(urlInput.value).toBe("not-a-url");
    });

    const submitButton = screen.getByRole("button", { name: /add channel/i });
    fireEvent.click(submitButton);

    await waitFor(
      () => {
        const errorText = screen.queryByText(/invalid.*url/i);
        expect(errorText).toBeTruthy();
      },
      { timeout: 3000 }
    );
    expect(mockOnSuccess).not.toHaveBeenCalled();
  });
});
