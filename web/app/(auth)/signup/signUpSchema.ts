import { z } from "zod";

/**
 * Sign-up form schema. Mirrors the backend's
 * <c>[Required, EmailAddress] string Email</c>,
 * <c>[Required, MinLength(8)] string Password</c>,
 * <c>[Required] string DisplayName</c>, and
 * <c>[Required] string TimeZone</c> on
 * <c>AuthController.SignUpDto</c> (the backend requires all
 * four; the form supplies a default for <c>timeZone</c>).
 */
export const signUpSchema = z.object({
  email: z.string().min(1, "Email is required").email("Enter a valid email"),
  password: z.string().min(8, "Use at least 8 characters"),
  displayName: z.string().min(1, "Display name is required").max(120, "Too long"),
  timeZone: z.string().min(1, "Pick a time zone"),
});

export type SignUpFormValues = z.infer<typeof signUpSchema>;
