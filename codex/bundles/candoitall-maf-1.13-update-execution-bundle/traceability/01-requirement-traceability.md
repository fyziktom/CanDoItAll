# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `RQ-001` | `bundle://inputs/00-original-request.md` | `subbundles/01-sb01-inventory-and-freeze` | Source artifact index and copied input tree. | Original prep preserved. |
| `RQ-002` | `bundle://analysis/01-current-state.md` | `subbundles/01-sb01-inventory-and-freeze` | `git status`, package list, restore/build baseline transcripts. | Must separate pre-existing failures. |
| `RQ-003` | `bundle://inputs/original-prep/docs/02-nuget-update-inventory.md` | `subbundles/02-sb02-package-version-update` | Package diff and restore transcript. | Stable MAF only. |
| `RQ-004` | `bundle://analysis/01-current-state.md` | `subbundles/02-sb02-package-version-update` | Restore/build dependency-floor evidence. | Do not chase latest. |
| `RQ-005` | `bundle://analysis/01-current-state.md` | `subbundles/02-sb02-package-version-update` | NuGet CLI outdated transcript and package decision table. | A2A and Mem0 are evidence-gated. |
| `RQ-006` | `bundle://inputs/original-prep/docs/03-breaking-change-risk-map.md` | `subbundles/03-sb03-compile-break-adapter-compatibility` | Build transcript and changed-file source assertions. | Adapter seams only. |
| `RQ-007` | `bundle://inputs/original-prep/docs/05-validation-and-regression-plan.md` | `subbundles/03-sb03-compile-break-adapter-compatibility`, `subbundles/05-sb05-focused-regression-validation` | Focused tests, source assertions, semantic invariants. | Governance invariants. |
| `RQ-008` | `bundle://inputs/original-prep/docs/01-current-architecture-map.md` | `subbundles/04-sb04-architecture-drift-checkpoint`, `subbundles/06-sb06-evidence-and-merge-readiness` | Source scans for process tool provider and route expansion. | Expected historical mentions must be distinguished. |
| `RQ-009` | `bundle://architecture/00-csharp-current-state-inventory.md` | `subbundles/04-sb04-architecture-drift-checkpoint` | Architecture drift review and CodeAnalytics/dependency proof when needed. | Blocks fake separation. |
| `RQ-010` | `bundle://inputs/original-prep/docs/05-validation-and-regression-plan.md` | `subbundles/05-sb05-focused-regression-validation` | Focused and broad test transcripts plus skip notes. | App works as before or better. |
| `RQ-011` | `bundle://inputs/original-prep/docs/04-codex-execution-plan.md` | `subbundles/06-sb06-evidence-and-merge-readiness` | `docs/maf-1.13-update-evidence.md` and final execution report rows. | Merge readiness. |
| `RQ-012` | `bundle://inputs/00-original-request.md` | Preparation | `bundle://checklists/maf-1.13-phase-checklists.xlsx`. | Workbook artifact. |
