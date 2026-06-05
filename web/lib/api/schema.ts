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
};

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
