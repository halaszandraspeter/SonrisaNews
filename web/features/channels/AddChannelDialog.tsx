"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Stack,
  TextField,
  Alert,
  Box,
  Typography,
  CircularProgress,
} from "@mui/material";
import { z } from "zod";
import { apiClient } from "@/lib/api/client";
import { CreateChannelRequest } from "@/lib/api/schema";

// Zod schemas for validation
const emailChannelSchema = z.object({
  type: z.literal("email"),
  destination: z.string().email("Invalid email address"),
});

const slackChannelSchema = z.object({
  type: z.literal("slack"),
  destination: z.string().url("Invalid webhook URL"),
});

const channelSchema = z.union([emailChannelSchema, slackChannelSchema]);

type ChannelInput = z.infer<typeof channelSchema>;

type Step = "select-type" | "enter-destination" | "verify" | "success";

/**
 * One error state for the dialog. Either a validation error (the
 * user typed something invalid) or an API error (the server
 * rejected the request). Rendered as a single <c>Alert</c> at the
 * top of the dialog.
 */
type DialogError =
  | { kind: "validation"; message: string }
  | { kind: "api"; message: string }
  | null;

interface AddChannelDialogProps {
  open: boolean;
  onClose: () => void;
  /**
   * Called after the channel is successfully verified. Carries
   * the new channel's id so the parent can invalidate the right
   * query key (e.g. <c>["channels"]</c>).
   */
  onChannelCreated: (channelId: string) => void;
}

export function AddChannelDialog({
  open,
  onClose,
  onChannelCreated,
}: AddChannelDialogProps) {
  const [step, setStep] = useState<Step>("select-type");
  const [selectedType, setSelectedType] = useState<"email" | "slack" | null>(null);
  const [destination, setDestination] = useState("");
  const [verificationCode, setVerificationCode] = useState("");
  // The codegen produces a non-nullable <c>channel.Id: string</c>.
  // <c>null</c> is the "not yet created" sentinel before the
  // create mutation succeeds.
  const [channelId, setChannelId] = useState<string | null>(null);
  const [error, setError] = useState<DialogError>(null);

  // Mutation order: dependencies come first so the file reads top-to-bottom.
  // `confirmVerify` does not depend on any other mutation; `startVerify`
  // depends on the channelId passed as a variable.
  const confirmVerifyMutation = useMutation({
    mutationFn: async (vars: { channelId: string; code: string }) => {
      const { response, error } = await apiClient.POST(
        "/api/v1/channels/{id}/verify/confirm",
        { params: { path: { id: vars.channelId } }, body: { Code: vars.code } }
      );
      if (error || !response.ok) {
        throw new Error("Verification failed");
      }
      return response.body;
    },
    onSuccess: (_, vars) => {
      setStep("success");
      setError(null);
      onChannelCreated(vars.channelId);
    },
    onError: () => {
      setError({ kind: "api", message: "Verification failed. Please check the code and try again." });
    },
  });

  const startVerifyMutation = useMutation({
    mutationFn: async (vars: { channelId: string }) => {
      const { response, error } = await apiClient.POST(
        "/api/v1/channels/{id}/verify/start",
        { params: { path: { id: vars.channelId } } }
      );
      if (error || !response.ok) {
        throw new Error("Failed to start verification");
      }
      return response.body;
    },
    onSuccess: () => {
      setStep("verify");
    },
    onError: () => {
      setError({ kind: "api", message: "Failed to send verification code. Please try again." });
    },
  });

  const createChannelMutation = useMutation({
    mutationFn: async (req: CreateChannelRequest) => {
      const { data, response, error } = await apiClient.POST("/api/v1/channels", {
        body: req,
      });
      if (error || !response.ok || !data) {
        throw new Error("Failed to create channel");
      }
      return data;
    },
    onSuccess: (channel) => {
      // Pass the new channel id as a variable rather than
      // reading it from state. The mutation no longer closes
      // over a stale <c>channelId</c> if a future refactor
      // adds an <c>await</c> between the two steps.
      const newId = channel.Id;
      setChannelId(newId);
      setError(null);
      startVerifyMutation.mutate({ channelId: newId });
    },
    onError: () => {
      setError({ kind: "api", message: "Failed to create channel. Please try again." });
    },
  });

  const handleClose = () => {
    setStep("select-type");
    setSelectedType(null);
    setDestination("");
    setVerificationCode("");
    setChannelId(null);
    setError(null);
    onClose();
  };

  const handleSelectType = (type: "email" | "slack") => {
    setSelectedType(type);
    setStep("enter-destination");
    setError(null);
  };

  const handleCreateChannel = async () => {
    setError(null);

    if (!selectedType) {
      setError({ kind: "validation", message: "Please select a channel type" });
      return;
    }

    const data = { type: selectedType, destination };

    const result = channelSchema.safeParse(data);
    if (!result.success) {
      const message = result.error.issues[0]?.message ?? "Invalid input";
      setError({ kind: "validation", message });
      return;
    }

    createChannelMutation.mutate({
      Type: selectedType,
      Destination: destination,
    });
  };

  const handleConfirmVerification = async () => {
    setError(null);
    if (!channelId || !verificationCode) {
      setError({ kind: "validation", message: "Please enter the verification code" });
      return;
    }
    confirmVerifyMutation.mutate({ channelId, code: verificationCode });
  };

  const handleBack = () => {
    if (step === "enter-destination") {
      setStep("select-type");
      setSelectedType(null);
      setDestination("");
      setError(null);
    } else if (step === "verify") {
      setStep("enter-destination");
      setVerificationCode("");
      setError(null);
    }
  };

  const isLoading =
    createChannelMutation.isPending ||
    startVerifyMutation.isPending ||
    confirmVerifyMutation.isPending;

  return (
    <Dialog
      open={open}
      onClose={(_, reason) => {
        // Close on backdrop click, escape, etc.
        handleClose();
      }}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle>Add a Channel</DialogTitle>
      <DialogContent>
        <Stack spacing={3} sx={{ mt: 2 }}>
          {error !== null ? (
            <Alert severity="error">{error.message}</Alert>
          ) : null}

          {step === "select-type" && (
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                Choose where you'd like to receive notifications
              </Typography>
              <Stack direction="row" spacing={2}>
                <Button
                  variant="outlined"
                  onClick={() => handleSelectType("email")}
                  fullWidth
                >
                  Email
                </Button>
                <Button
                  variant="outlined"
                  onClick={() => handleSelectType("slack")}
                  fullWidth
                >
                  Slack
                </Button>
              </Stack>
            </Stack>
          )}

          {step === "enter-destination" && (
            <Stack spacing={2}>
              <TextField
                label={selectedType === "email" ? "Email address" : "Slack webhook URL"}
                placeholder={
                  selectedType === "email"
                    ? "Enter your email address"
                    : "Enter webhook URL"
                }
                value={destination}
                onChange={(e) => {
                  setDestination(e.target.value);
                  setError(null);
                }}
                fullWidth
                autoFocus
              />
            </Stack>
          )}

          {step === "verify" && (
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                A verification code has been sent to {destination}. Enter it below to
                confirm.
              </Typography>
              <TextField
                label="Verification code"
                placeholder="Enter the 6-digit code"
                value={verificationCode}
                onChange={(e) => {
                  setVerificationCode(e.target.value);
                  setError(null);
                }}
                fullWidth
                autoFocus
              />
            </Stack>
          )}

          {step === "success" && (
            <Box sx={{ textAlign: "center", py: 2 }}>
              <Typography variant="h6">Channel verified!</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                Your channel is ready to receive notifications.
              </Typography>
            </Box>
          )}
        </Stack>
      </DialogContent>

      <DialogActions>
        {step === "success" ? (
          <Button onClick={handleClose} variant="contained">
            Done
          </Button>
        ) : (
          <>
            {step !== "select-type" && (
              <Button onClick={handleBack} disabled={isLoading}>
                Back
              </Button>
            )}
            <Button onClick={handleClose} disabled={isLoading}>
              Cancel
            </Button>
            {step === "enter-destination" && (
              <Button
                onClick={handleCreateChannel}
                variant="contained"
                disabled={isLoading || !destination}
              >
                {isLoading ? <CircularProgress size={24} /> : "Add channel"}
              </Button>
            )}
            {step === "verify" && (
              <Button
                onClick={handleConfirmVerification}
                variant="contained"
                disabled={isLoading || !verificationCode}
              >
                {isLoading ? <CircularProgress size={24} /> : "Confirm"}
              </Button>
            )}
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
