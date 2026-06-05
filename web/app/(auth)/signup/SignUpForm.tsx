"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { type ReactElement } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";

import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";

import { apiClient } from "@/lib/api/client";
import { useAuthStore } from "@/lib/auth/authStore";
import { signUpSchema, type SignUpFormValues } from "./signUpSchema";

/**
 * Sign-up form. Posts to <c>POST /api/v1/auth/signup</c>; the
 * response shape is the same as sign-in (access token + httpOnly
 * refresh cookie). The email-verification step is post-signup
 * (handled by the verify page); the new user lands in
 * <c>UserStatus.PendingEmailVerification</c> and can use the app
 * but with a "please verify your email" nudge.
 */
export function SignUpForm(): ReactElement {
  const router = useRouter();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<SignUpFormValues>({
    resolver: zodResolver(signUpSchema),
    // Default the time zone to the browser's detected zone (via
    // <c>Intl.DateTimeFormat</c>) rather than the hard-coded "UTC".
    // A user in <c>Europe/Budapest</c> shouldn't have to know the
    // IANA id to get digest emails at the right hour. The MVP
    // handoff defers full i18n to wave 9; this is the smallest
    // possible UX win that doesn't pull in a dependency.
    defaultValues: {
      email: "",
      password: "",
      displayName: "",
      timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    },
  });

  const onSubmit: SubmitHandler<SignUpFormValues> = async (values) => {
    const { data, response, error } = await apiClient.POST("/api/v1/auth/signup", {
      body: {
        Email: values.email,
        Password: values.password,
        DisplayName: values.displayName,
        TimeZone: values.timeZone,
      },
    });    if (error || !response.ok) {
      const title =
        response.status === 409
          ? "An account with this email already exists"
          : "Sign-up failed";
      setError("root", { type: "server", message: title });
      return;
    }
    if (data === undefined) {
      setError("root", { type: "server", message: "Sign-up failed" });
      return;
    }
    useAuthStore.getState().setSession(data);
    router.push("/alerts");
  };

  return (
    <Stack component="form" spacing={2} onSubmit={handleSubmit(onSubmit)} noValidate>
      <TextField
        label="Email"
        type="email"
        autoComplete="email"
        required
        error={errors.email !== undefined}
        helperText={errors.email?.message}
        {...register("email")}
      />
      <TextField
        label="Password"
        type="password"
        autoComplete="new-password"
        required
        error={errors.password !== undefined}
        helperText={errors.password?.message ?? "At least 8 characters"}
        {...register("password")}
      />
      <TextField
        label="Display name"
        autoComplete="nickname"
        required
        error={errors.displayName !== undefined}
        helperText={errors.displayName?.message}
        {...register("displayName")}
      />
      <TextField
        label="Time zone"
        required
        error={errors.timeZone !== undefined}
        helperText={errors.timeZone?.message}
        {...register("timeZone")}
      />
      {errors.root !== undefined ? (
        <Typography role="alert" color="error">
          {errors.root.message}
        </Typography>
      ) : null}
      <Button type="submit" variant="contained" disabled={isSubmitting}>
        {isSubmitting ? "Creating account…" : "Create account"}
      </Button>
    </Stack>
  );
}
