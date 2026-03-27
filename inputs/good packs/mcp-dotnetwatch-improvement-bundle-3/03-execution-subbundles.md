# Execution Sub-Bundles

This file turns bundle 3 into implementation-sized work packages with explicit acceptance gates.

## Sub-Bundle 1: Tray Shell

Goal:

- provide a Windows tray icon that reflects backend state and opens the existing backend manager page

Files:

- `subbundles/01-tray-shell-checklist.md`

Acceptance gate:

- tray icon runs locally, shows status, and opens the manager page for the current workspace

## Sub-Bundle 2: Health And Duplicate Detection

Goal:

- detect operator-relevant failure states early and surface them without needing an active Codex session

Files:

- `subbundles/02-health-and-duplicate-checklist.md`

Acceptance gate:

- tray app surfaces missing, duplicate, and unreachable backend states and supports recovery actions

## Sub-Bundle 3: Resetup And Cross-PC Packaging

Goal:

- make the current MCP path, tray app, and repo-managed Codex skill easy to reinstall or update on other PCs

Files:

- `subbundles/03-resetup-and-packaging-checklist.md`

Acceptance gate:

- one resetup script updates the wrapper launch path, installed tools, and repo-managed skill pack

## Sub-Bundle 4: Validation

Goal:

- prove tray functionality and confirm no meaningful hot-reload performance regression

Files:

- `subbundles/04-validation-checklist.md`

Acceptance gate:

- test and benchmark evidence is written back into this bundle
