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

interface AddChannelDialogProps {
  open: boolean;
  onClose: () => void;
  onChannelCreated: () => void;
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
  const [channelId, setChannelId] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string>("");
  const [apiError, setApiError] = useState<string>("");

  // Mutation order: dependencies come first so the file reads top-to-bottom.
  // `confirmVerify` does not depend on any other mutation; `startVerify`
  // depends on `channelId` (set by `createChannel`); `createChannel` calls
  // `startVerify` in its `onSuccess` after `setChannelId(channel.Id)` flushes.
  const confirmVerifyMutation = useMutation({
    mutationFn: async (code: string) => {
      if (!channelId) throw new Error("Channel ID not set");
      const { response, error } = await apiClient.POST(
        "/api/v1/channels/{id}/verify/confirm",
        { params: { path: { id: channelId } }, body: { Code: code } }
      );
      if (error || !response.ok) {
        throw new Error("Verification failed");
      }
      return response.body;
    },
    onSuccess: () => {
      setStep("success");
      setApiError("");
      onChannelCreated();
    },
    onError: () => {
      setApiError("Verification failed. Please check the code and try again.");
    },
  });

  const startVerifyMutation = useMutation({
    mutationFn: async () => {
      if (!channelId) throw new Error("Channel ID not set");
      const { response, error } = await apiClient.POST(
        "/api/v1/channels/{id}/verify/start",
        { params: { path: { id: channelId } } }
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
      setApiError("Failed to send verification code. Please try again.");
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
      // setChannelId is flushed in the same React batch as the
      // mutate() call, so startVerifyMutation's closure sees the new
      // channelId when its mutationFn runs.
      setChannelId(channel.Id);
      startVerifyMutation.mutate();
      setApiError("");
    },
    onError: () => {
      setApiError("Failed to create channel. Please try again.");
    },
  });

  const handleClose = () => {
    setStep("select-type");
    setSelectedType(null);
    setDestination("");
    setVerificationCode("");
    setChannelId(null);
    setValidationError("");
    setApiError("");
    onClose();
  };

  const handleSelectType = (type: "email" | "slack") => {
    setSelectedType(type);
    setStep("enter-destination");
    setValidationError("");
  };

  const handleCreateChannel = async () => {
    setValidationError("");

    if (!selectedType) {
      setValidationError("Please select a channel type");
      return;
    }

    const data = { type: selectedType, destination };

    const result = channelSchema.safeParse(data);
    if (!result.success) {
      setValidationError(result.error.issues[0]?.message ?? "Invalid input");
      return;
    }

    createChannelMutation.mutate({
      Type: selectedType,
      Destination: destination,
    });
  };

  const handleConfirmVerification = async () => {
    if (!verificationCode) {
      setValidationError("Please enter the verification code");
      return;
    }
    confirmVerifyMutation.mutate(verificationCode);
  };

  const handleBack = () => {
    if (step === "enter-destination") {
      setStep("select-type");
      setSelectedType(null);
      setDestination("");
      setValidationError("");
    } else if (step === "verify") {
      setStep("enter-destination");
      setVerificationCode("");
      setValidationError("");
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
          {apiError && <Alert severity="error">{apiError}</Alert>}
          {validationError && <Alert severity="error">{validationError}</Alert>}

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
                  setValidationError("");
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
                  setValidationError("");
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
