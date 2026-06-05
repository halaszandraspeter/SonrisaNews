"use client";

/**
 * Client-side host for the alert editor dialog. Owns the
 * dialog's open-state and the alert being edited (or
 * <c>undefined</c> for create mode). Both <c>NewAlertButton</c>
 * (page header) and <c>AlertList</c> (per-row Edit) call into
 * this host instead of each instantiating their own dialog —
 * keeping a single source of truth means the
 * "create → edit the new alert" workflow is one continuous
 * session.
 *
 * Children: anything that wants to open the editor. Each child
 * is a tiny button that calls <c>openForCreate</c> or
 * <c>openForEdit</c> via the context.
 */

import { createContext, useCallback, useContext, useMemo, useState } from "react";

import { AlertEditorDialog } from "./AlertEditorDialog";
import type { AlertResponse } from "@/lib/api/schema";

type EditorState =
  | { mode: "closed" }
  | { mode: "create" }
  | { mode: "edit"; alert: AlertResponse };

type EditorHostContextValue = {
  state: EditorState;
  openForCreate: () => void;
  openForEdit: (alert: AlertResponse) => void;
  close: () => void;
};

const EditorHostContext = createContext<EditorHostContextValue | null>(null);

export function useAlertEditorHost(): EditorHostContextValue {
  const ctx = useContext(EditorHostContext);
  if (ctx === null) {
    throw new Error(
      "useAlertEditorHost must be used inside <AlertsEditorHost>",
    );
  }
  return ctx;
}

export type AlertsEditorHostProps = {
  children: React.ReactNode;
};

export function AlertsEditorHost({ children }: AlertsEditorHostProps) {
  const [state, setState] = useState<EditorState>({ mode: "closed" });

  const openForCreate = useCallback(() => {
    setState({ mode: "create" });
  }, []);

  const openForEdit = useCallback((alert: AlertResponse) => {
    setState({ mode: "edit", alert });
  }, []);

  const close = useCallback(() => {
    setState({ mode: "closed" });
  }, []);

  const value = useMemo<EditorHostContextValue>(
    () => ({ state, openForCreate, openForEdit, close }),
    [state, openForCreate, openForEdit, close],
  );

  return (
    <EditorHostContext.Provider value={value}>
      {children}
      <AlertEditorDialog
        open={state.mode !== "closed"}
        onClose={close}
        onSaved={close}
        alert={state.mode === "edit" ? state.alert : undefined}
      />
    </EditorHostContext.Provider>
  );
}
