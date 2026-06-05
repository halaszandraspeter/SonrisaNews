"use client";

/**
 * Client-side body for <c>/alerts</c>. Owns the
 * <c>AlertsEditorHost</c> so the page-header "New alert"
 * button and the per-row "Edit" actions in <c>AlertList</c>
 * share a single editor dialog. The page itself stays a
 * Server Component (h1, description, and the "Wave 5" banner
 * are static).
 */

import Stack from "@mui/material/Stack";

import { AlertList } from "./AlertList";
import { AlertsEditorHost } from "./AlertsEditorHost";
import { NewAlertButton } from "./NewAlertButton";
import { AddChannelButton } from "@/features/channels/AddChannelButton";

export function AlertsPageBody() {
  return (
    <AlertsEditorHost>
      <Stack spacing={4}>
        <Stack direction="row" spacing={2}>
          <NewAlertButton />
          <AddChannelButton />
        </Stack>
        <AlertList />
      </Stack>
    </AlertsEditorHost>
  );
}
