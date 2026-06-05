"use client";

/**
 * React Query hooks for the wave-5 alert dashboard. Query keys
 * are exported from <c>@/lib/api/keys</c> so any feature can
 * invalidate the right slice without depending on this file
 * (and without creating a feature-to-feature import edge).
 *
 *   ['alerts']                                — list (all)
 *   ['alerts', alertId]                       — single alert
 *   ['alerts', alertId, 'channels']           — channel-mode rows for an alert
 *   ['channels']                              — caller's channel list
 *
 * Mutation hooks invalidate the smallest set needed. The
 * channel-mode mutations invalidate ['alerts', alertId,
 * 'channels'] (not the whole ['alerts'] tree).
 */

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  createAlert,
  deleteAlert,
  listAlerts,
  listChannelModes,
  listChannels,
  removeChannelMode,
  setChannelMode,
  testAlert,
  updateAlert,
} from "./api";
import {
  alertChannelsQueryKey,
  alertQueryKey,
  alertsQueryKey,
  channelsQueryKey,
} from "@/lib/api/keys";
import type {
  AlertChannelModeResponse,
  AlertResponse,
  ChannelResponse,
  CreateAlertRequest,
  SetChannelModeRequest,
  TestAlertHit,
  UpdateAlertRequest,
} from "@/lib/api/schema";

export function useAlertsQuery() {
  return useQuery<AlertResponse[], Error>({
    queryKey: alertsQueryKey,
    queryFn: async () => {
      const result = await listAlerts();
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
    staleTime: 30_000,
  });
}

export function useAlertChannelModesQuery(alertId: string) {
  return useQuery<AlertChannelModeResponse[], Error>({
    queryKey: alertChannelsQueryKey(alertId),
    queryFn: async () => {
      const result = await listChannelModes(alertId);
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
    enabled: alertId.length > 0,
  });
}

export function useChannelsQuery() {
  return useQuery<ChannelResponse[], Error>({
    queryKey: channelsQueryKey,
    queryFn: async () => {
      const result = await listChannels();
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
    staleTime: 30_000,
  });
}

export function useCreateAlertMutation() {
  const qc = useQueryClient();
  return useMutation<AlertResponse, Error, CreateAlertRequest>({
    mutationFn: async (body) => {
      const result = await createAlert(body);
      if (result.kind === "validation") {
        throw new ValidationError(result.fields);
      }
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: alertsQueryKey });
    },
  });
}

export function useUpdateAlertMutation(alertId: string) {
  const qc = useQueryClient();
  return useMutation<AlertResponse, Error, UpdateAlertRequest>({
    mutationFn: async (body) => {
      const result = await updateAlert(alertId, body);
      if (result.kind === "validation") {
        throw new ValidationError(result.fields);
      }
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
    onSuccess: (data) => {
      qc.setQueryData(alertQueryKey(alertId), data);
      void qc.invalidateQueries({ queryKey: alertsQueryKey });
    },
  });
}

export function useDeleteAlertMutation() {
  const qc = useQueryClient();
  return useMutation<void, Error, string>({
    mutationFn: async (alertId) => {
      const result = await deleteAlert(alertId);
      if (result.kind !== "ok") throw new Error(toMessage(result));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: alertsQueryKey });
    },
  });
}

export function useSetChannelModeMutation(alertId: string) {
  const qc = useQueryClient();
  return useMutation<AlertChannelModeResponse, Error, SetChannelModeRequest>({
    mutationFn: async (body) => {
      const result = await setChannelMode(alertId, body);
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: alertChannelsQueryKey(alertId) });
    },
  });
}

export function useRemoveChannelModeMutation(alertId: string) {
  const qc = useQueryClient();
  return useMutation<void, Error, string>({
    mutationFn: async (channelId) => {
      const result = await removeChannelMode(alertId, channelId);
      if (result.kind !== "ok") throw new Error(toMessage(result));
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: alertChannelsQueryKey(alertId) });
    },
  });
}

export function useTestAlertMutation(alertId: string) {
  return useMutation<TestAlertHit[], Error, void>({
    mutationFn: async () => {
      const result = await testAlert(alertId);
      if (result.kind !== "ok") throw new Error(toMessage(result));
      return result.data;
    },
  });
}

// --- Errors --------------------------------------------------------------

/**
 * Thrown by the alert mutations when the backend returns a
 * <c>ValidationProblemDetails</c> body. The form's <c>onError</c>
 * reads <c>fields</c> to map backend errors back to form fields.
 */
export class ValidationError extends Error {
  fields: Record<string, string[]>;
  constructor(fields: Record<string, string[]>) {
    super("Validation failed");
    this.name = "ValidationError";
    this.fields = fields;
  }
}

const toMessage = (result: { kind: string; status?: number; message?: string }): string => {
  if (result.kind === "http") {
    return result.message ?? `HTTP ${result.status ?? 0}`;
  }
  if (result.kind === "network") {
    return "Network error";
  }
  if (result.kind === "validation") {
    return "Validation failed";
  }
  return "Unknown error";
};
