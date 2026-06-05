import { render, screen, fireEvent } from "@testing-library/react";
import { vi, describe, it, expect } from "vitest";
import { ChannelModeMatrix } from "./ChannelModeMatrix";
import type { AlertResponse, ChannelResponse, AlertChannelModeResponse } from "@/lib/api/schema";

const ALERT: AlertResponse = {
  Id: "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  Name: "Test alert",
  Type: "News",
  Enabled: true,
  Filters: "{}",
  CreatedAt: "2026-06-05T00:00:00Z",
  UpdatedAt: "2026-06-05T00:00:00Z",
};

const CHANNELS: ChannelResponse[] = [
  {
    Id: "c1c2c3c4-1111-4111-8111-111111111111",
    Type: "Email",
    Destination: "ada@example.com",
    Verified: true,
    CreatedAt: "2026-06-05T00:00:00Z",
  },
  {
    Id: "c2c2c3c4-2222-4222-8222-222222222222",
    Type: "Slack",
    Destination: "https://hooks.slack.com/services/T00/B00/XX",
    Verified: true,
    CreatedAt: "2026-06-05T00:00:00Z",
  },
];

describe("ChannelModeMatrix", () => {
  it("renders a column header per channel and a row for the alert", () => {
    render(
      <ChannelModeMatrix
        alert={ALERT}
        channels={CHANNELS}
        modes={[]}
        onChange={vi.fn()}
        isUpdating={false}
      />,
    );
    // Channel destinations are visible as the column headers.
    expect(screen.getByText("ada@example.com")).toBeTruthy();
    expect(screen.getByText("https://hooks.slack.com/services/T00/B00/XX")).toBeTruthy();
    // Alert name is in the row label.
    expect(screen.getByText("Test alert")).toBeTruthy();
  });

  it("shows the em-dash placeholder when no mode row exists for the channel", () => {
    render(
      <ChannelModeMatrix
        alert={ALERT}
        channels={CHANNELS}
        modes={[]}
        onChange={vi.fn()}
        isUpdating={false}
      />,
    );
    // Two em-dashes (one per channel) — the placeholder text.
    const dashes = screen.getAllByText("—");
    expect(dashes.length).toBe(2);
  });

  it("renders the existing mode label when a row exists", () => {
    const modes: AlertChannelModeResponse[] = [
      {
        AlertId: ALERT.Id,
        ChannelId: CHANNELS[0]!.Id,
        Mode: "Digest15m",
        CreatedAt: "2026-06-05T00:00:00Z",
      },
    ];
    render(
      <ChannelModeMatrix
        alert={ALERT}
        channels={CHANNELS}
        modes={modes}
        onChange={vi.fn()}
        isUpdating={false}
      />,
    );
    // The configured mode's label is rendered for the first
    // channel; the second still shows the em-dash.
    expect(screen.getByText("Digest (15m)")).toBeTruthy();
    expect(screen.getAllByText("—").length).toBe(1);
  });

  it("does NOT call onChange when the user picks 'Don't deliver' on a missing-row cell", () => {
    const onChange = vi.fn();
    render(
      <ChannelModeMatrix
        alert={ALERT}
        channels={CHANNELS}
        modes={[]}
        onChange={onChange}
        isUpdating={false}
      />,
    );
    // Open the first channel's dropdown and pick "Don't deliver".
    const firstSelect = screen.getAllByRole("combobox")[0] as HTMLElement;
    fireEvent.mouseDown(firstSelect);
    const dontDeliver = screen.getAllByText("Don't deliver")[0] as HTMLElement;
    fireEvent.click(dontDeliver);
    // The previous value was undefined (no row), so the
    // matrix must treat the pick as a no-op — no DELETE on
    // the server.
    expect(onChange).not.toHaveBeenCalled();
  });

  it("DOES call onChange with null when the user picks 'Don't deliver' on a configured cell", () => {
    const onChange = vi.fn();
    const modes: AlertChannelModeResponse[] = [
      {
        AlertId: ALERT.Id,
        ChannelId: CHANNELS[0]!.Id,
        Mode: "Realtime",
        CreatedAt: "2026-06-05T00:00:00Z",
      },
    ];
    render(
      <ChannelModeMatrix
        alert={ALERT}
        channels={CHANNELS}
        modes={modes}
        onChange={onChange}
        isUpdating={false}
      />,
    );
    const firstSelect = screen.getAllByRole("combobox")[0] as HTMLElement;
    fireEvent.mouseDown(firstSelect);
    const dontDeliver = screen.getAllByText("Don't deliver")[0] as HTMLElement;
    fireEvent.click(dontDeliver);
    expect(onChange).toHaveBeenCalledWith(CHANNELS[0]!.Id, null);
  });

  it("calls onChange with the new mode when the user picks one of the delivery modes", () => {
    const onChange = vi.fn();
    render(
      <ChannelModeMatrix
        alert={ALERT}
        channels={CHANNELS}
        modes={[]}
        onChange={onChange}
        isUpdating={false}
      />,
    );
    const firstSelect = screen.getAllByRole("combobox")[0] as HTMLElement;
    fireEvent.mouseDown(firstSelect);
    const realtime = screen.getAllByText("Realtime")[0] as HTMLElement;
    fireEvent.click(realtime);
    expect(onChange).toHaveBeenCalledWith(CHANNELS[0]!.Id, "Realtime");
  });
});
