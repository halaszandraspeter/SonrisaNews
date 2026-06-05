import { z } from "zod";

/**
 * Sign-in form schema. Mirrors the backend's
 * <c>[Required, EmailAddress] string Email</c> and
 * <c>[Required] string Password</c> attributes on
 * <c>AuthController.SignInDto</c>.
 */
export const signInSchema = z.object({
  email: z.string().min(1, "Email is required").email("Enter a valid email"),
  password: z.string().min(1, "Password is required"),
});

export type SignInFormValues = z.infer<typeof signInSchema>;
