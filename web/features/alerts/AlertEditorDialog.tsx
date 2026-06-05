"use client";

/**
 * Create / edit alert dialog. Single dialog handles both modes
 * (create when <c>alert</c> is undefined; edit when supplied).
 *
 * The dialog is the only consumer of the alert-mutation hooks for
 * create/update. Delete lives in the parent list row.
 *
 * Form state: a plain React <c>useState</c> for the form fields and
 * a separate <c>filtersJson</c> string for the type-specific filter
 * shape. The Zod schema for the selected type parses the string
 * for the sub-form to edit, and re-serializes to canonical JSON
 * on submit. This keeps the editor a single string field per
 * state slot, which makes the submit path trivial.
 *
 * Server errors come back typed (the alert controller returns
 * <c>ValidationProblemDetails</c> on filter validation). The
 * mutation throws <c>ValidationError</c>; the dialog maps the
 * fields to a single inline error.
 */

import { useEffect, useMemo, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import FormControl from "@mui/material/FormControl";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormHelperText from "@mui/material/FormHelperText";
import FormLabel from "@mui/material/FormLabel";
import Radio from "@mui/material/Radio";
import RadioGroup from "@mui/material/RadioGroup";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";

import { AlertFilterFields } from "./AlertFilterFields";
import { alertTypeLabel, emptyFiltersFor, type AlertType } from "./alertTypes";
import { canonicalize, isTypeFilterEmpty, parseFilters } from "./filterSchemas";
import {
  useCreateAlertMutation,
  useUpdateAlertMutation,
  ValidationError,
} from "./useAlertsQueries";
import type { AlertResponse } from "@/lib/api/schema";

export type AlertEditorDialogProps = {
  open: boolean;
  onClose: () => void;
  onSaved: (alert: AlertResponse) => void;
  /** Undefined = create mode. */
  alert?: AlertResponse;
};

export function AlertEditorDialog({
  open,
  onClose,
  onSaved,
  alert,
}: AlertEditorDialogProps) {
  const isEdit = alert !== undefined;

  const [name, setName] = useState("");
  const [type, setType] = useState<AlertType>("News");
  const [enabled, setEnabled] = useState(true);
  const [filtersJson, setFiltersJson] = useState<string>(emptyFiltersFor("News"));
  const [apiError, setApiError] = useState<string>("");
  // Per-field, per-error-array. Multiple backend messages for the
  // same field are joined with " · " so the form's
  // <c>helperText</c> shows them all (the first one is
  // <c>errors[0]</c>, the rest are appended).
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  // Hydrate the form when entering edit mode (or when the alert
  // prop changes). Reset when closing.
  useEffect(() => {
    if (!open) return;
    if (alert !== undefined) {
      setName(alert.Name);
      setType(alert.Type);
      setEnabled(alert.Enabled);
      setFiltersJson(alert.Filters);
    } else {
      setName("");
      setType("News");
      setEnabled(true);
      setFiltersJson(emptyFiltersFor("News"));
    }
    setApiError("");
    setFieldErrors({});
  }, [open, alert]);

  // When the user changes the type, reset the filter document to
  // that type's empty shape. (Editing an existing alert and
  // changing type is allowed but rare; we keep the reset
  // behavior because the filter shape is type-specific.)
  const handleTypeChange = (next: AlertType) => {
    setType(next);
    setFiltersJson(emptyFiltersFor(next));
    setFieldErrors({});
  };

  const createMutation = useCreateAlertMutation();
  const updateMutation = useUpdateAlertMutation(alert?.Id ?? "");

  const parsed = useMemo(() => parseFilters(type, filtersJson), [type, filtersJson]);
  const filterInvalid = !parsed.success;
  const filterMessage = parsed.success ? "" : parsed.message;

  const submitDisabled =
    name.trim().length === 0 ||
    filterInvalid ||
    (parsed.success && isTypeFilterEmpty(type, parsed.data)) ||
    createMutation.isPending ||
    updateMutation.isPending;

  const handleSubmit = () => {
    if (filterInvalid || !parsed.success) {
      setFieldErrors({ filters: [filterMessage] });
      return;
    }
    setApiError("");
    setFieldErrors({});

    // The backend stores filters as a JSON string. We send the
    // canonical form (re-serialized from the typed object) so
    // the column on disk is always in the same shape.
    const canonicalFilters = canonicalize(type, parsed.data);

    // Two onSuccess callbacks run in order: the hook-level one
    // invalidates the queries that show the alert, the
    // per-call one closes the editor. Both must run — the
    // hook-level is set up at mutation-construction time
    // (useAlertsQueries.ts) and is fixed for the lifetime of
    // the component; the per-call one is the dialog's local
    // "save succeeded, dismiss" hook. Splitting them keeps the
    // list-invalidation in the hook and the dialog UX in the
    // dialog.
    if (isEdit) {
      updateMutation.mutate(
        { Name: name.trim(), Filters: canonicalFilters, Enabled: enabled },
        {
          onSuccess: (saved) => onSaved(saved),
          onError: (err) => {
            if (err instanceof ValidationError) {
              setFieldErrors(err.fields);
              setApiError(
                "The alert has validation errors. Please fix them and try again.",
              );
              return;
            }
            // Non-validation errors (network, 5xx, plain 4xx
            // with a string body) show the message verbatim —
            // they don't have a per-field map.
            setApiError(err.message);
          },
        },
      );
    } else {
      createMutation.mutate(
        { Name: name.trim(), Type: type, Filters: canonicalFilters },
        {
          onSuccess: (saved) => onSaved(saved),
          onError: (err) => {
            if (err instanceof ValidationError) {
              setFieldErrors(err.fields);
              setApiError(
                "The alert has validation errors. Please fix them and try again.",
              );
              return;
            }
            setApiError(err.message);
          },
        },
      );
    }
  };

  const handleClose = () => {
    if (createMutation.isPending || updateMutation.isPending) return;
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={(_, reason) => {
        // Block backdrop click while a mutation is in flight.
        if (createMutation.isPending || updateMutation.isPending) return;
        if (reason === "backdropClick") return;
        handleClose();
      }}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle>{isEdit ? "Edit alert" : "New alert"}</DialogTitle>
      <DialogContent>
        <Stack spacing={3} sx={{ mt: 1 }}>
          {apiError !== "" ? <Alert severity="error">{apiError}</Alert> : null}

          <TextField
            label="Name"
            placeholder="e.g. Breaking news — Japan"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            fullWidth
            autoFocus
            error={fieldErrors["name"] !== undefined}
            helperText={fieldErrors["name"]?.join(" · ")}
          />

          <FormControl disabled={isEdit}>
            <FormLabel>Type</FormLabel>
            <RadioGroup
              row
              value={type}
              onChange={(e) => handleTypeChange(e.target.value as AlertType)}
            >
              {(["News", "Market", "Disaster"] as const).map((t) => (
                <FormControlLabel
                  key={t}
                  value={t}
                  control={<Radio />}
                  label={alertTypeLabel[t]}
                />
              ))}
            </RadioGroup>
            {isEdit ? (
              <FormHelperText>The alert type can&apos;t be changed after creation.</FormHelperText>
            ) : null}
          </FormControl>

          <AlertFilterFields
            type={type}
            filtersJson={filtersJson}
            onFiltersJsonChange={setFiltersJson}
          />

          {fieldErrors["filters"] !== undefined ? (
            <Typography variant="caption" color="error">
              {fieldErrors["filters"].join(" · ")}
            </Typography>
          ) : null}

          {isEdit ? (
            <FormControlLabel
              control={
                <Checkbox
                  checked={enabled}
                  onChange={(e) => setEnabled(e.target.checked)}
                />
              }
              label="Enabled (the matcher will skip this alert when off)"
            />
          ) : null}

          <Box>
            <Typography variant="caption" color="text.secondary">
              Alerts are inert until the matcher wires them up in wave 6.
            </Typography>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={createMutation.isPending || updateMutation.isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={submitDisabled}
        >
          {createMutation.isPending || updateMutation.isPending ? (
            <CircularProgress size={20} />
          ) : isEdit ? (
            "Save"
          ) : (
            "Create"
          )}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
