# Requirement Traceability

| Requirement | Bundle files | Owning subbundle | Proof row |
| --- | --- | --- | --- |
| `R001` | `inputs/00-original-request.md`, `reviews/01-execution-report.md` | SB04 | Raw note closure and validators |
| `R002` | `inventories/01-scope-inventory.md`, `architecture/01-target-solution.md` | SB01 | SB01 gate row |
| `R003` | `inventories/01-scope-inventory.md`, `plan/01-phase-plan.md` | SB01 | Build command |
| `R004` | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | SB01 | Source review and build |
| `R005` | `architecture/01-target-solution.md`, `requirements/01-normalized-requirements.md` | SB02 | Package install tests |
| `R006` | `inputs/02-structured-input.md`, `architecture/01-target-solution.md` | SB02 | Package zip tests |
| `R007` | `architecture/01-target-solution.md`, `requirements/01-normalized-requirements.md` | SB02 | Invalid manifest test |
| `R008` | `analysis/02-assumptions-and-risks.md`, `architecture/01-target-solution.md` | SB02 | Traversal rejection test |
| `R009` | `architecture/01-target-solution.md` | SB02 | Catalog visibility test |
| `R010` | `architecture/01-target-solution.md` | SB02 | Startup loader test or build/service scan proof |
| `R011` | `architecture/01-target-solution.md` | SB02 | Restart status test |
| `R012` | `subbundles/03-03-plugins-ui-package-install-and-restart/README.md` | SB03 | Component/browser proof |
| `R013` | `subbundles/03-03-plugins-ui-package-install-and-restart/README.md` | SB03 | Component/browser proof |
| `R014` | `subbundles/03-03-plugins-ui-package-install-and-restart/README.md` | SB03 | Restart service/API/UI proof |
| `R015` | `reviews/01-execution-report.md` | SB04 | Existing targeted plugin tests |

## Raw Note Closure Plan

| Raw note | Owning subbundle | Planned proof |
| --- | --- | --- |
| `N001` | SB04 | Bundle validators and report updates |
| `N002` | SB01 | Source split and registration diff |
| `N003` | SB02, SB03 | Runtime package install tests and UI proof |
| `N004` | SB02 | Zip install and startup loader tests |
| `N005` | SB02, SB03 | Restart-required state proof |
| `N006` | SB03 | Restart action test and browser proof |
| `N007` | SB01 | `src/plugins` projects in solution |
| `N008` | SB04 | Existing plugin tests pass |
| `N009` | SB03 | Catalogue install UI proof |
| `N010` | SB02, SB03 | Zip manifest/libs/icon test and upload UI proof |
