# Execution Sub-Bundles

This file turns bundle 2 into implementation-sized work packages with explicit acceptance gates.

Status:

- completed on March 25, 2026
- final measured hot-reload loop is `8.145s` on `PageHeader.razor` and `11.703s` on `ProjectsPage.razor`
- final evidence is recorded in `08-implementation-results.md` and `artifacts/final-watch-benchmark-summary.json`

## Sub-Bundle 1: SourceWatch Parity

Goal:

- make managed `SourceWatch` behave like plain local `dotnet watch` for simple UI edits

Files:

- `subbundles/01-sourcewatch-parity-checklist.md`

Acceptance gate:

- managed watch simple text edit is visible in about 15 seconds

## Sub-Bundle 2: Runtime Confirmation

Goal:

- stop treating `Hot reload succeeded` as proof that the runtime-visible change exists

Files:

- `subbundles/02-runtime-confirmation-checklist.md`

Acceptance gate:

- `RevisionConfirmed` does not succeed until the runtime generation actually advances

## Sub-Bundle 3: Managed Build Fast Path

Goal:

- reduce warm managed build overhead without losing MCP ergonomics

Files:

- `subbundles/03-managed-build-fastpath-checklist.md`

Acceptance gate:

- warm managed builds are materially closer to plain local builds than the old isolated-artifacts flow

## Sub-Bundle 4: Validation

Goal:

- verify the full flow with repeatable scripts and current repo integration tests

Files:

- `subbundles/04-validation-checklist.md`

Acceptance gate:

- benchmark and test evidence is written back into this bundle
