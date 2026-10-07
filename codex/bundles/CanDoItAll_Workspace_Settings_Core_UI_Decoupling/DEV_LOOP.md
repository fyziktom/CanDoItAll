# Development-loop proof for Workspace Settings Core

The outcome is a small useful UI loop, not a promised acceleration of the full Web host.
Take the baseline after S0 and before moving Settings renderers. Use owned synthetic defaults,
secrets, associations and history rows; do not open the user's actual Secrets UI for evidence.

## Capture the actual execution context

Record current repository SHA/status, SDK, configuration, OS, source-versus-package mode,
Components/FileTools/SharedInfo revisions, chosen viewport and supplied asset mode. Record
all evaluated project/package/native edges and unresolved/cycle checks for original Web,
extracted Web and the new sandbox. Evaluate `Directory.Build.targets` source replacement.
A direct csproj list or reflection check alone is not the complete build graph.

Record watch inventories using the actual host and configuration. Keep feature C#, Razor,
scoped CSS, shared component source and authoritative theme inputs visible. A linked generated
CSS input is content, not permission to reference the full Web project. Explain missing-theme
rebuild instructions; do not regenerate a second competing theme.

## Compare meaningful visible changes

Use the same selected sections and representative fixture scale. Startup-to-interactive is
separate from subsequent edits. Capture multiple startup observations where feasible; label
single observations honestly. Capture at least three successful samples for each applicable
edit class and preserve failed attempts separately:

| Edit | Observable proof |
| --- | --- |
| Razor | Change an actual section label or renderer heading, observe it in the browser. |
| C# | Change an executed pure presentation projection or validation text and drive the real action that uses it. |
| Scoped CSS | Change computed geometry on a real field/section and observe computed style. |
| Feature JS, if introduced/changed | Execute the changed module callback; distinguish refresh/import from hot reload. |

No new feature-specific JS is required. Existing SecretField/copy/dialog modules still require
asset and behavior proof even when JS benchmarking is not applicable. Do not count process
restart/startup or a changed source timestamp as an edit-to-visible sample. Record unsupported
edits, required browser refresh and restarts as such.

Inspect the production/sandbox desktop at the current supported large viewport (the reviewed
implementation used 1600x1000). Preserve readable sections, visible actions, actual secret mask,
policy confirmation and one intentional scroll owner. Avoid cosmetic redesign unrelated to
extraction. Focus and input caret must not jump under a background reference refresh.

## Source, runtime and published graph proof are different

Build the sandbox directly. Run it without database/vault/API/driver configuration. Render all
four extracted sections with actual child controls. Publish the standalone sandbox and run its
DLL in Production. Check static response status, generated scoped styles, theme/font, dialog
modules, console/page errors and real interactions. Do not use the main Web host as a fallback
when a sandbox asset is missing.

The deferred three local sections can be absent/labelled out of scope in the sandbox; they
must remain real and reachable in production. Their absence does not excuse missing renderers
from the four selected sections. Do not claim an entire Workspace graph cut.

## Per-interaction I/O budget

Typing, local filters, masking and tab navigation must not trigger database/provider/secret
payload reads per keystroke. Header metadata reads are distinct from plaintext resolution.
History tab entry has zero policy reads until explicit Load. File preference reads may invoke
the existing owner's lazy migration behavior; preserve and disclose it rather than asserting
an invented globally side-effect-free read contract. A retry after a confirmed write must not
replay that write. Use counters and actual owner tests, not a subjective responsiveness claim.

## Isolation and reporting

Use an isolated configuration such as `WorkspaceSettingsUiProof` and task-owned ports. Preserve
probe source bytes, restore in finally, and stop only owned process handles/trees. Never modify
or stop the ordinary app at 5032, personal control-plane file, installed vault or user database.

Document graph counts, conditions, raw sample arrays and medians if useful. Report actual
startup and full-Web variance; a larger full-Web graph after adding projects is not a failed
sandbox extraction. Do not compare synthetic scenario startup to PostgreSQL Web as a pure
compiler benchmark or turn one run into a statistical speedup promise.
