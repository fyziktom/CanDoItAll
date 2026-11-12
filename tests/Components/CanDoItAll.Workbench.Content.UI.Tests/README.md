# Workbench Content UI tests

Independent bUnit tests mount the actual Content UI controls without application services.
They cover immutable text drafts, bounded upload reads, retirement, exact typed receipts,
duplicate events and uncertain dispatch. File controls use actual FileInteraction children
and revisioned save ports. Summary, transcript, strict Mermaid and image tests cover actual
renderers, explicit confirmation, encoded source, safe labels, routed model IDs and independent
openings. Sandbox presentation does not substitute for provider or storage authority. Native storage, admission and revision tests live
in the main Components suite.

Build this project, check discovery with `dotnet test --list-tests`, then run
`dotnet test --no-build --no-restore` with the same configuration. The tests are part of
the Components and Stable solutions and the explicit CI component lists.
