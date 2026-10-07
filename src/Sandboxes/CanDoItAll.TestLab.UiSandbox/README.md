# TestLab sandbox

Standalone host for the production TestLab renderer. It registers interactive Blazor and
BaseLib only; no database, full application DI, provider host or backend module is required.
Projects and parties are minimal fixture options. Fake aggregate storage is separate from
the selected draft: an admitted delayed write updates its original stored aggregate even
after selection changes, without patching the successor. Switching scenarios releases all
controlled waits; Retry refresh only reads committed fake data.

Reference requests have their own generation and controlled waits. Changing the responsible
party leaves a committed save's read-back pending until **Complete next delayed action**;
completion retains the newer choice and admits a later explicit save. Retiring an editor
or project target cannot settle a successor's submission.

The bounded party catalog separates existence from project membership. A known saved party
remains named for a global plan or a project whose choices omit it; unknown or deliberately
unavailable IDs stay visible as unavailable. `DelayedPartyLookup` holds saved-reference
resolution, and `ReferenceFailure` shows a failed lookup without clearing the stored ID.
MissingReferences removes the fake catalog entry as well as retiring the project binding.

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.TestLab.UiSandbox
```

The scenario selector covers representative, empty, filtered, large, missing plan and
reference, failed/stale read, invalid editor, delayed plan/reference/save/read-back,
committed warning, refresh failure, unknown write and admission refusal states. Use
**Complete next delayed action** to release one outstanding operation. The counter shows
pending operations separately from actual fake commits. All four sections use the shipped
renderer and its normal controls, form validation and reconciliation policy.

Parity mode links the generated Web stylesheet as content and loads BaseLib's real styles,
icons and Blazor assets. It adds no compile reference to Web. If that stylesheet is absent,
run the repository Tailwind build; the sandbox build fails explicitly rather than using
an incomplete substitute. Source dependency mode still requires the normal sibling roots.

See [architecture and proof](../../../docs/architecture/testlab-ui-boundary.md).
