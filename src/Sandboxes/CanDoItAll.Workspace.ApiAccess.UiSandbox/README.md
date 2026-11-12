# Workspace API Access sandbox

This independent host mounts the production API Access renderers and controllers over
bounded in-memory owners. It requires no database, vault, signing key, account file or
production service registration. `NOT-A-CREDENTIAL` disclosure values cannot authenticate.
The `fixture.*` vocabulary is deliberately separate from the production scope catalog.

From the repository root, generate the existing Parity theme if needed, then run:

```powershell
npm ci --prefix Tailwind
npm run tailwind:build
dotnet watch --project src/Sandboxes/CanDoItAll.Workspace.ApiAccess.UiSandbox --configuration Debug --no-launch-profile -- --urls http://127.0.0.1:5098
```

Choose an unused loopback port. The CSS content link uses the Web theme without a Web
project reference. BaseLib supplies the real inputs, tables, scope dialogs, confirmations,
clipboard module, fonts and shared styles. No feature-specific stylesheet or script is needed.

Scenarios include representative, empty and 61-record pages; denied, unavailable and
disabled/misconfigured status. Select an operation to hold its next access check, read,
write or observation. Set a typed outcome to exercise refusal, conflict, missing record,
postcommit warning or an unknown acknowledgement before/after the synthetic commit.
Release through the control bar or **Alt+Shift+R** while a modal is open. Change accounts
externally to demonstrate version conflicts, including a change during an admitted hold.

Reset creates an independent store and retires the old view. Already admitted writes
complete against their original store; late values never populate the successor view.
Each store holds at most 128 account/token records, at most 16 operation holds can be
queued, and at most eight retired stores are retained. Pending stores are not evicted to
make room. The safe operation ledger holds 32 entries and never evicts unresolved writes.
Exact observations are read-only and neither establish causality nor clear unknown locks.

The ordinary user editor supports versioned create/edit/reset/delete and empty grants.
Machine metadata supports lazy search, 25-item paging, revoke and delete. Scope cancel
preserves raw text. Passwords and one-time values are retired with their owning view.
Fixture policy is only scenario behavior; real authorization, durability and HTTP/session
enforcement are tested separately against the production owners.

The owning light tests are `tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests`.
Source and published-Production browser journeys are `ApiAccessSandboxBrowserTests`.
See [the boundary record](../../../docs/architecture/workspace-api-access-ui-boundary.md)
for proof selection, dependency measurements and limitations.
