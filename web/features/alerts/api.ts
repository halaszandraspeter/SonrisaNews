/**
 * Typed wrappers around the OpenAPI client for the alert + channel
 * endpoints used by the wave-5 dashboard. The wrappers exist so
 * the React Query hooks and the editor can share the same call
 * sites; the type-narrowing (e.g. <c>paths["..."]</c> indexing) is
 * in one place.
 *
 * Errors: every wrapper returns a discriminated union so the
 * caller doesn't have to know about openapi-fetch's <c>{ data,
 * error, response }</c> triple. The four useful states are:
 *   - <c>{ kind: "ok", data }</c>
 *   - <c>{ kind: "http", status, message }</c> — any non-2xx
 *   - <c>{ kind: "validation", fields }</c> — 400 with a
 *     <c>ValidationProblemDetails</c> body (the alert endpoints
 *     use this for filter errors)
 *   - <c>{ kind: "network", error }</c> — fetch threw (the
 *     401-refresh middleware has already tried to recover)
 */

import { apiClient } from "@/lib/api/client";
import type {
  AlertChannelModeResponse,
  AlertResponse,
  AlertTestResponse,
  ChannelResponse,
  CreateAlertRequest,
  SetChannelModeRequest,
  UpdateAlertRequest,
} from "@/lib/api/schema";

export type ApiResult<T> =
  | { kind: "ok"; data: T }
  | { kind: "http"; status: number; message: string }
  | { kind: "validation"; fields: Record<string, string[]> }
  | { kind: "network"; error: unknown };

// openapi-fetch returns a result-object (not a thrown Promise)
// with { data, error, response }. Wrap it in our discriminated
// union so callers can match on <c>kind</c> instead of knowing
// openapi-fetch's shape. All fields are optional in the
// openapi-fetch types.
interface OpenApiResult<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

const unwrap = async <T>(resultPromise: Promise<OpenApiResult<T>>): Promise<ApiResult<T>> => {
  try {
    const { data, response, error } = await resultPromise;
    if (response.ok) {
      return { kind: "ok", data: data as T };
    }
    // Look for a ValidationProblemDetails body on any
    // 4xx. The alert controller returns 400 with an
    // <c>errors</c> map for filter validation; some
    // endpoints (e.g. ASP.NET's built-in
    // <c>[ApiController]</c> model validation) return 400
    // or 422 with the same shape. The plain
    // <c>RejectEmptyGuid</c> branch returns 400 with a
    // bare string — that path falls through to the
    // generic <c>http</c> kind.
    if (response.status >= 400 && response.status < 500) {
      try {
        const body = (await response.clone().json()) as {
          errors?: Record<string, string[]>;
        };
        if (
          body.errors !== undefined &&
          typeof body.errors === "object" &&
          Object.keys(body.errors).length > 0
        ) {
          return { kind: "validation", fields: body.errors };
        }
      } catch {
        // Body was not JSON; fall through to the generic http error.
      }
    }
    return {
      kind: "http",
      status: response.status,
      message:
        typeof error === "object" && error !== null && "message" in error
          ? String((error as { message: unknown }).message)
          : `HTTP ${response.status}`,
    };
  } catch (err) {
    return { kind: "network", error: err };
  }
};

// --- Alerts --------------------------------------------------------------

export const listAlerts = (): Promise<ApiResult<AlertResponse[]>> =>
  unwrap<AlertResponse[]>(apiClient.GET("/api/v1/alerts"));

export const getAlert = (id: string): Promise<ApiResult<AlertResponse>> =>
  unwrap<AlertResponse>(apiClient.GET("/api/v1/alerts/{id}", { params: { path: { id } } }));

export const createAlert = (body: CreateAlertRequest): Promise<ApiResult<AlertResponse>> =>
  unwrap<AlertResponse>(apiClient.POST("/api/v1/alerts", { body }));

export const updateAlert = (
  id: string,
  body: UpdateAlertRequest,
): Promise<ApiResult<AlertResponse>> =>
  unwrap<AlertResponse>(apiClient.PUT("/api/v1/alerts/{id}", { params: { path: { id } }, body }));

export const deleteAlert = (id: string): Promise<ApiResult<void>> =>
  unwrap<void>(apiClient.DELETE("/api/v1/alerts/{id}", { params: { path: { id } } }));

// --- Alert × channel matrix --------------------------------------------

export const listChannelModes = (alertId: string): Promise<ApiResult<AlertChannelModeResponse[]>> =>
  unwrap<AlertChannelModeResponse[]>(
    apiClient.GET("/api/v1/alerts/{id}/channels", { params: { path: { id: alertId } } }),
  );

export const setChannelMode = (
  alertId: string,
  body: SetChannelModeRequest,
): Promise<ApiResult<AlertChannelModeResponse>> =>
  unwrap<AlertChannelModeResponse>(
    apiClient.POST("/api/v1/alerts/{id}/channels", { params: { path: { id: alertId } }, body }),
  );

export const removeChannelMode = (
  alertId: string,
  channelId: string,
): Promise<ApiResult<void>> =>
  unwrap<void>(
    apiClient.DELETE("/api/v1/alerts/{id}/channels/{channelId}", {
      params: { path: { id: alertId, channelId } },
    }),
  );

// --- Test alert (stub until wave 6) ------------------------------------

export const testAlert = (alertId: string): Promise<ApiResult<AlertTestResponse>> =>
  unwrap<AlertTestResponse>(
    apiClient.POST("/api/v1/alerts/{id}/test", { params: { path: { id: alertId } } }),
  );

// --- Channels -----------------------------------------------------------

export const listChannels = (): Promise<ApiResult<ChannelResponse[]>> =>
  unwrap<ChannelResponse[]>(apiClient.GET("/api/v1/channels"));
