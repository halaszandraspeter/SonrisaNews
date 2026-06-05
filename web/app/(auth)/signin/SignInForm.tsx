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
import { signInSchema, type SignInFormValues } from "./signInSchema";

/**
 * Sign-in form. Posts to <c>POST /api/v1/auth/signin</c>; on success
 * the API sets the httpOnly refresh cookie and returns the access
 * token in the body. The form persists the token in the auth store
 * and redirects to <c>/alerts</c> (the dashboard placeholder lands
 * in wave 4).
 *
 * Server errors are surfaced as a top-level Alert via
 * <c>formState.errors.root</c>; 401 is the only expected server error
 * and gets a friendlier copy than 5xx.
 */
export function SignInForm(): ReactElement {
  const router = useRouter();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit: SubmitHandler<SignInFormValues> = async (values) => {
    const { data, response, error } = await apiClient.POST("/api/v1/auth/signin", {
      body: { Email: values.email, Password: values.password },
    });
    if (error || !response.ok) {
      const title = response.status === 401 ? "Wrong email or password" : "Sign-in failed";
      setError("root", { type: "server", message: title });
      return;
    }
    if (data === undefined) {
      setError("root", { type: "server", message: "Sign-in failed" });
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
        autoComplete="current-password"
        required
        error={errors.password !== undefined}
        helperText={errors.password?.message}
        {...register("password")}
      />
      {errors.root !== undefined ? (
        <Typography role="alert" color="error">
          {errors.root.message}
        </Typography>
      ) : null}
      <Button type="submit" variant="contained" disabled={isSubmitting}>
        {isSubmitting ? "Signing in…" : "Sign in"}
      </Button>
    </Stack>
  );
}
