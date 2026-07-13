# Requirement Traceability

## Raw Note Closure Matrix

| Raw Note | Normalized Requirements | Owning Subbundle | Planned Proof |
| --- | --- | --- | --- |
| N001 | MAF2-R001, MAF2-R009, MAF2-R014 | SB01, SB08 | Baseline inventory plus final boundary scan and behavior proof. |
| N002 | MAF2-R002, MAF2-R003, MAF2-R004 | SB02, SB03, SB04 | DTO extraction, composition extraction, builder extraction tests. |
| N003 | MAF2-R004, MAF2-R006, MAF2-R008, MAF2-R010 | SB04, SB05, SB06 | Named collaborators, no broad manager/service-locator review. |
| N004 | MAF2-R001, MAF2-R012 | SB01, SB07 | Hidden type inventory and guard tests. |
| N005 | MAF2-R009, MAF2-R010, MAF2-R012 | SB03, SB07, SB08 | folder/type ownership review and source scans. |
| N006 | MAF2-R006, MAF2-R007, MAF2-R008, MAF2-R011 | SB05, SB06, SB07 | Direct collaborator tests with fakes. |
| N007 | MAF2-R010, MAF2-R012 | SB07 | Guard tests prevent new runtime partial/nested builder sprawl. |
| N008 | All | Bundle preparation only | Prepared-stage validator output. |

## Requirement To Artifact Map

| Requirement | Bundle Files | Implementation Phase |
| --- | --- | --- |
| MAF2-R001 | `analysis/01-current-state.md`, `inventories/01-scope-inventory.md`, `subbundles/01-01-current-state-hidden-runtime-map/README.md` | SB01 |
| MAF2-R002 | `architecture/01-target-solution.md`, `subbundles/02-02-runtime-contracts-and-configuration-dtos/README.md` | SB02 |
| MAF2-R003 | `architecture/01-target-solution.md`, `subbundles/03-03-capability-composition-coordinator/README.md` | SB03 |
| MAF2-R004 | `inventories/01-scope-inventory.md`, `subbundles/04-04-capability-builder-extractions/README.md` | SB04 |
| MAF2-R005 | `subbundles/04-04-capability-builder-extractions/README.md` | SB04 |
| MAF2-R006 | `subbundles/05-05-workspace-input-and-artifact-drivers/README.md` | SB05 |
| MAF2-R007 | `subbundles/05-05-workspace-input-and-artifact-drivers/README.md`, `subbundles/06-06-execution-finalizer-and-recovery-drivers/README.md` | SB05/SB06 |
| MAF2-R008 | `subbundles/06-06-execution-finalizer-and-recovery-drivers/README.md` | SB06 |
| MAF2-R009 | `architecture/01-target-solution.md`, `subbundles/08-08-performance-and-final-closure/README.md` | SB08 |
| MAF2-R010 | `reviews/00-bundle-self-review.md`, all subbundle `Do Not Do` sections | All |
| MAF2-R011 | `subbundles/07-07-test-harness-and-architecture-guards/README.md` | SB07 |
| MAF2-R012 | `subbundles/07-07-test-harness-and-architecture-guards/README.md` | SB07 |
| MAF2-R013 | `subbundles/08-08-performance-and-final-closure/README.md` | SB08 |
| MAF2-R014 | `reviews/01-execution-report.md`, `subbundles/08-08-performance-and-final-closure/README.md` | SB08 |
