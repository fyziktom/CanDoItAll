# TestLab sandbox

Standalone host for the production TestLab renderer. It registers interactive Blazor and
BaseLib only; no database, full application DI, provider host or backend module is required.
Projects and parties are minimal fixture options. Fake aggregate storage is separate from
the selected draft: an admitted delayed write updates its original stored aggregate even
after selection changes, without patching the successor. Switching scenarios releases all
controlled waits; Retry refresh only reads committed fake data.

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
