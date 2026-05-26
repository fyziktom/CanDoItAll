You are working in `fyziktom/CanDoItAll`, branch `processes-hardening`.

Use this bundle:

`codex/bundles/maf16-process-run-recovery-upgrade-bundle-v1`

Execute in order.

Hard rules:

1. Start with the captured failed run evidence under `codex/bundles/process-run-first-step-artifact-binding-failure-inputs-v1`.
2. Upgrade Microsoft Agent Framework from 1.3 to 1.6.x before fixing process runtime behavior.
3. Resolve exact NuGet versions from package search / restore. Prefer 1.6.2 for stable packages where available, but do not invent a version for packages that only exist as preview or renamed packages.
4. Treat the A2A package as high risk: release notes mention A2A v1.0 migration and the current project uses `Microsoft.Agents.AI.A2A` 1.3 preview.
5. Do not remove process governance checks to make tests pass.
6. Fix artifact binding so a current-run artifact is accepted only when it is genuinely current, content-backed, and lineage-valid.
7. Keep the process core generic. Tetris and Blazor belong in templates/tests, not core logic.
8. Preserve PostgreSQL-only assumptions.
9. After every major phase, run focused build/tests and update proof manifests.
