# Workspace: extraction complete, behavior not yet closed

R03/R04 describe the completed remaining leaves: StorageRecovery.UI, DataSources.UI and the
neutral Configuration.UI renderer, each with a real standalone sandbox. Earlier Core,
ApiAccess, StorageCatalog and StorageSelection leaves remain protected. Preserve that design.

| Surface | Current responsibility | Closure work |
|---|---|---|
| Settings shell/defaults/Secrets/Files/history | Existing controllers and active slots | Retest real owners and secret/lifetime paths; no new extraction |
| API Access | Independent instance-local control-plane leaf | Retest authorization, token/password cleanup and independent profile behavior |
| Storage Catalog | Independent catalog editor, stage receipts and write fence | Preserve SCAT-R1 and actual routing/health semantics |
| Storage Selection | Lightweight metadata/read-only picker | Preserve Apply vs Agent Save and exact runtime allowlists |
| Storage Recovery | New leaf and existing original-intent owner adapter | Recheck real Workflow/process continuation and shared Dialog |
| Data Sources | New leaf and control-plane adapter | WCL-R1 exact write fix, two-profile/schema/transfer/restart regression |
| Configuration fallback | New neutral schema renderer in existing Configuration.UI | Preserve raw validation and identity; no service/credential lookup |
| SettingsRendererHost | Intentional trusted registry/compatibility host | Preserve owner/trust/schema mismatch refusal; no permissive fallback |
| MainLayout database UI | Intentional Web shell integration | WC-C3 lifecycle correction; not a wholesale MainLayout extraction |

Do a final recursive census of Workspace Razor/code-behind/CSS/JS plus actual external
renderers and dynamic registrations. Reconcile each with the existing JSON map and true
consumer reference graph. A thin route/DI/auth/registry host may stay in a backend module;
a backend-dependent reusable renderer hidden below it may not. Retain the precise reason.

Do not treat the map's `workspace_ui_complete=true` as proof of WCL-R1 correctness or of
application readiness. Add the new finding and its eventual closure evidence to the relevant
entry. Keep `application_regression_ready=false` until the required proof is actually closed.
No report should imply that Processes or the entire Workbench renderer family has also been
extracted by this Workspace assignment.
