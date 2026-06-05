/**
 * OpenAPI-generated types. Hand-authored for the wave-3 frontend to
 * keep the codegen surface typed without requiring a live API at PR
 * time. Run `pnpm generate:api` to overwrite this file from the
 * backend's `/openapi/v1.json` (see `package.json` for the script).
 *
 * The shape is the standard `openapi-typescript` 7.x output. Each
 * operation has a `parameters` and a `responses` block. The
 * response bodies are typed as the C# DTO shape (Guid → `string` on
 * the wire; `DateTimeOffset` → ISO 8601 `string`).
 *
 * If the real codegen diverges from this file, the
 * `pnpm generate:api:check` script (added in this PR) fails CI before
 * merge. Drift between this file and the live API is a real frontend
 * bug, not a stylistic difference.
 */

export type paths = {
  "/api/v1/auth/signup": {
    post: operations["AuthController_SignUp"];
  };
  "/api/v1/auth/verify": {
    post: operations["AuthController_Verify"];
  };
  "/api/v1/auth/signin": {
    post: operations["AuthController_SignIn"];
  };
  "/api/v1/auth/refresh": {
    post: operations["AuthController_Refresh"];
  };
  "/api/v1/auth/signout": {
    post: operations["AuthController_SignOut"];
  };
  "/api/v1/auth/forgot": {
    post: operations["AuthController_Forgot"];
  };
  "/api/v1/auth/reset": {
    post: operations["AuthController_Reset"];
  };
  "/api/v1/me": {
    get: operations["MeController_Get"];
  };
  "/api/v1/me/permissions": {
    get: operations["MeController_GetPermissions"];
  };
  "/api/v1/health": {
    get: operations["HealthController_Get"];
  };
  "/api/v1/channels": {
    get: operations["ChannelsController_List"];
    post: operations["ChannelsController_Create"];
  };
  "/api/v1/channels/{id}": {
    get: operations["ChannelsController_Get"];
    delete: operations["ChannelsController_Delete"];
  };
  "/api/v1/channels/{id}/verify/start": {
    post: operations["ChannelsController_StartVerify"];
  };
  "/api/v1/channels/{id}/verify/confirm": {
    post: operations["ChannelsController_ConfirmVerify"];
  };
  "/api/v1/alerts": {
    get: operations["AlertsController_List"];
    post: operations["AlertsController_Create"];
  };
  "/api/v1/alerts/{id}": {
    get: operations["AlertsController_Get"];
    put: operations["AlertsController_Update"];
    delete: operations["AlertsController_Delete"];
  };
  "/api/v1/alerts/{id}/channels": {
    get: operations["AlertsController_ListChannelModes"];
    post: operations["AlertsController_SetChannelMode"];
  };
  "/api/v1/alerts/{id}/channels/{channelId}": {
    delete: operations["AlertsController_RemoveChannelMode"];
  };
  "/api/v1/alerts/{id}/test": {
    post: operations["AlertsController_Test"];
  };
};

export type operations = {
  // --- AuthController -----------------------------------------------------
  AuthController_SignUp: {
    responses: {
      /** 201 — sign-up succeeded; refresh cookie set; access token in body. */
      201: {
        content: {
          "application/json": SignUpResponse;
        };
      };
      /** 400 — input validation failed. */
      400: unknown;
      /** 409 — email already in use. */
      409: unknown;
    };
    requestBody: {
      content: {
        "application/json": SignUpDto;
      };
    };
  };
  AuthController_Verify: {
    responses: {
      204: never;
      400: unknown;
    };
    requestBody: {
      content: {
        "application/json": VerifyDto;
      };
    };
  };
  AuthController_SignIn: {
    responses: {
      200: {
        content: {
          "application/json": SignInResponse;
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
    };
    requestBody: {
      content: {
        "application/json": SignInDto;
      };
    };
  };
  AuthController_Refresh: {
    responses: {
      200: {
        content: {
          "application/json": SignInResponse;
        };
      };
      401: unknown;
    };
  };
  AuthController_SignOut: {
    responses: {
      204: never;
    };
  };
  AuthController_Forgot: {
    responses: {
      204: never;
    };
    requestBody: {
      content: {
        "application/json": ForgotDto;
      };
    };
  };
  AuthController_Reset: {
    responses: {
      204: never;
      400: unknown;
    };
    requestBody: {
      content: {
        "application/json": ResetDto;
      };
    };
  };
  // --- MeController -------------------------------------------------------
  MeController_Get: {
    responses: {
      200: {
        content: {
          "application/json": MeResponse;
        };
      };
      401: unknown;
      403: unknown;
    };
  };
  MeController_GetPermissions: {
    responses: {
      200: {
        content: {
          "application/json": PermissionsResponse;
        };
      };
      401: unknown;
      403: unknown;
    };
  };
  // --- HealthController ---------------------------------------------------
  HealthController_Get: {
    responses: {
      200: {
        content: {
          "application/json": HealthResponse;
        };
      };
    };
  };
  // --- ChannelsController --------------------------------------------------
  ChannelsController_Create: {
    responses: {
      /** 201 — channel created; verification required before use. */
      201: {
        content: {
          "application/json": ChannelResponse;
        };
      };
      /** 400 — invalid channel type or malformed destination. */
      400: unknown;
      /** 401 — not authenticated. */
      401: unknown;
      /** 403 — lacks permission. */
      403: unknown;
    };
    requestBody: {
      content: {
        "application/json": CreateChannelRequest;
      };
    };
  };
  ChannelsController_StartVerify: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      /** 200 — verification challenge issued. */
      200: {
        content: {
          "application/json": VerifyStartResponse;
        };
      };
      /** 400 — channel type not supported or implementation missing. */
      400: unknown;
      /** 401 — not authenticated. */
      401: unknown;
      /** 403 — lacks permission. */
      403: unknown;
      /** 404 — channel not found. */
      404: unknown;
    };
  };
  ChannelsController_ConfirmVerify: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      /** 204 — verification confirmed; channel is now verified. */
      204: never;
      /** 400 — verification failed (wrong code or unsupported type). */
      400: unknown;
      /** 401 — not authenticated. */
      401: unknown;
      /** 403 — lacks permission. */
      403: unknown;
      /** 404 — channel not found. */
      404: unknown;
    };
    requestBody: {
      content: {
        "application/json": VerifyConfirmRequest;
      };
    };
  };
  ChannelsController_Delete: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      /** 204 — channel deleted. */
      204: never;
      /** 401 — not authenticated. */
      401: unknown;
      /** 403 — lacks permission. */
      403: unknown;
      /** 404 — channel not found. */
      404: unknown;
    };
  };
  ChannelsController_Get: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      /** 200 — channel returned. */
      200: {
        content: {
          "application/json": ChannelResponse;
        };
      };
      401: unknown;
      403: unknown;
      404: unknown;
    };
  };
  /**
   * List the caller's channels. The wave 5 dashboard needs a
   * channel list to render the alert × channel matrix. The
   * backend's <c>ChannelsController</c> did not ship with this
   * endpoint in wave 4; the schema entry is added here so the
   * frontend can target it as soon as the backend adds it (a
   * one-line <c>HttpGet</c> + a <c>Where(c => c.UserId == userId)</c>
   * query). Until then, the query will receive a 404 from the API
   * and the matrix renders the empty state.
   */
  ChannelsController_List: {
    responses: {
      /** 200 — list of the caller's channels. */
      200: {
        content: {
          "application/json": ChannelResponse[];
        };
      };
      401: unknown;
      403: unknown;
    };
  };
  // --- AlertsController ---------------------------------------------------
  AlertsController_List: {
    responses: {
      /** 200 — caller's alerts, newest first. */
      200: {
        content: {
          "application/json": AlertResponse[];
        };
      };
      401: unknown;
      403: unknown;
    };
  };
  AlertsController_Get: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": AlertResponse;
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
    };
  };
  AlertsController_Create: {
    responses: {
      201: {
        content: {
          "application/json": AlertResponse;
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
    };
    requestBody: {
      content: {
        "application/json": CreateAlertRequest;
      };
    };
  };
  AlertsController_Update: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": AlertResponse;
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
    };
    requestBody: {
      content: {
        "application/json": UpdateAlertRequest;
      };
    };
  };
  AlertsController_Delete: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      204: never;
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
    };
  };
  AlertsController_ListChannelModes: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": AlertChannelModeResponse[];
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
    };
  };
  AlertsController_SetChannelMode: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": AlertChannelModeResponse;
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
    };
    requestBody: {
      content: {
        "application/json": SetChannelModeRequest;
      };
    };
  };
  AlertsController_RemoveChannelMode: {
    parameters: {
      path: {
        id: string;
        channelId: string;
      };
    };
    responses: {
      204: never;
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
    };
  };
  /**
   * "Test this alert" — re-runs the matcher against the most recent
   * 50 events for the alert. The matcher lands in wave 6; until
   * then the backend returns a 501 (Not Implemented) and the
   * frontend renders "Test runs after wave 6 ships".
   */
  AlertsController_Test: {
    parameters: {
      path: {
        id: string;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": AlertTestResponse;
        };
      };
      400: unknown;
      401: unknown;
      403: unknown;
      404: unknown;
      501: unknown;
    };
  };
};

// --- Channel DTOs --------------------------------------------------------

/** Body for `POST /api/v1/channels`. */
export type CreateChannelRequest = {
  Type: string;
  Destination: string;
};

/** Response shape for `POST /api/v1/channels` and `GET /api/v1/channels/{id}`. */
export type ChannelResponse = {
  Id: string;
  Type: string;
  Destination: string;
  Verified: boolean;
  CreatedAt: string;
};

/** Response shape for `POST /api/v1/channels/{id}/verify/start`. */
export type VerifyStartResponse = {
  Code: string;
};

/** Body for `POST /api/v1/channels/{id}/verify/confirm`. */
export type VerifyConfirmRequest = {
  Code: string;
};

// --- Alert DTOs ---------------------------------------------------------

/** Body for `POST /api/v1/alerts`. */
export type CreateAlertRequest = {
  Name: string;
  Type: AlertTypeString;
  /** JSON string. The shape depends on <c>Type</c>; see <c>features/alerts/filterSchemas</c>. */
  Filters?: string | null;
};

/** Body for `PUT /api/v1/alerts/{id}`. */
export type UpdateAlertRequest = {
  Name?: string | null;
  Filters?: string | null;
  Enabled?: boolean | null;
};

/** Body for `POST /api/v1/alerts/{id}/channels`. */
export type SetChannelModeRequest = {
  ChannelId: string;
  Mode: DeliveryModeString;
};

/** Response shape for an alert. */
export type AlertResponse = {
  Id: string;
  Name: string;
  Type: AlertTypeString;
  Enabled: boolean;
  /** JSON string. Parse with the per-type Zod schema before reading. */
  Filters: string;
  CreatedAt: string;
  UpdatedAt: string;
};

/** Response shape for an alert-channel-mode row. */
export type AlertChannelModeResponse = {
  AlertId: string;
  ChannelId: string;
  Mode: DeliveryModeString;
  CreatedAt: string;
};

/**
 * Response shape for `POST /api/v1/alerts/{id}/test`. Each match
 * is the would-have-fired event shape: title, source, occurred
 * time, and a snippet of body. The matcher (wave 6) populates
 * this; until then the backend returns 501.
 */
export type AlertTestResponse = {
  AlertId: string;
  MatchedCount: number;
  Matches: AlertTestMatch[];
};

export type AlertTestMatch = {
  EventId: string;
  Title: string;
  Source: string;
  OccurredAt: string;
  Snippet: string;
};

/** String form of the <c>AlertType</c> C# enum (System.Text.Json + JsonStringEnumConverter). */
export type AlertTypeString = "News" | "Market" | "Disaster";

/** String form of the <c>DeliveryMode</c> C# enum. */
export type DeliveryModeString = "Realtime" | "Digest15m" | "DigestHourly" | "DigestDaily";

// --- DTOs ----------------------------------------------------------------

/** Body for `POST /api/v1/auth/signup`. Mirrors `SignUpDto` on the backend. */
export type SignUpDto = {
  Email: string;
  Password: string;
  DisplayName?: string | null;
  TimeZone?: string | null;
};

/** Body for `POST /api/v1/auth/verify`. */
export type VerifyDto = {
  Token: string;
};

/** Body for `POST /api/v1/auth/signin`. */
export type SignInDto = {
  Email: string;
  Password: string;
};

/** Body for `POST /api/v1/auth/forgot`. */
export type ForgotDto = {
  Email: string;
};

/** Body for `POST /api/v1/auth/reset`. */
export type ResetDto = {
  Token: string;
  NewPassword: string;
};

/**
 * Response shape for sign-in. The refresh token is in the httpOnly
 * cookie, not the body. The role is intentionally absent (per the
 * 2026-06-05 user rule); the client asks `/api/v1/me/permissions`
 * for the granted permission set.
 */
export type SignInResponse = {
  UserId: string;
  Email: string;
  DisplayName: string;
  Status: string;
  AccessToken: string;
  AccessTokenExpiresAt: string;
};

/** Same shape as `SignInResponse`; kept separate to keep the OpenAPI doc stable. */
export type SignUpResponse = SignInResponse;

/** Response shape for `GET /api/v1/me`. */
export type MeResponse = {
  UserId: string;
  Email: string;
};

/** Response shape for `GET /api/v1/me/permissions`. */
export type PermissionsResponse = {
  Permissions: string[];
};

/** Response shape for `GET /api/v1/health`. */
export type HealthResponse = {
  Status: string;
  CheckedAt: string;
};
