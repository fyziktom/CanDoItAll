# Implementation Prompt

Implement only the current subbundle from `cdi_process_workspace_containment_bundle`.

Constraints:

- Keep the change inside existing BaseLib and processes-module patterns.
- Prefer `PageScaffold`, `ListDetailShell`, `Tabs`, and existing wrappers over new bespoke layout abstractions.
- Make the smallest correct containment fix that keeps the process workspace and templates modal inside the available height.
- If you touch a browser-visible surface, capture fresh browser proof before closing the subbundle.
