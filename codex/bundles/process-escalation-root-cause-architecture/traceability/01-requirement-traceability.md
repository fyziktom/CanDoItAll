# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R01 Runtime Diagnostics | `requirements/01-normalized-requirements.md` | `subbundles/01-runtime-diagnostics-lineage` | `dotnet test ... --filter Process` and API readback | Critical foundation. |
| R02 Artifact And Result Lineage | `requirements/01-normalized-requirements.md` | `subbundles/01-runtime-diagnostics-lineage` | Result/artifact lineage unit and integration tests | Required before recovery can be trusted. |
| R03 Capability Readiness Contract | `requirements/01-normalized-requirements.md` | `subbundles/02-capability-readiness-policy-model` | `dotnet test ... --filter Capability` | Must cover tools, MCPs, skills, suppressions, operations, and receipts. |
| R04 HR Matching And Preflight Surfacing | `requirements/01-normalized-requirements.md` | `subbundles/02-capability-readiness-policy-model` | Launch preview and integration proof | Detects missing access before run/dispatch. |
| R05 Driver-Owned Recovery | `requirements/01-normalized-requirements.md` | `subbundles/03-driver-owned-recovery-classification` | `dotnet test ... --filter Recovery` | Depends on SB01 and SB02. |
| R06 Domain Isolation | `requirements/01-normalized-requirements.md` | `subbundles/04-dotnet-delivery-driver-isolation` | Domain leak scan and architecture tests | Generic runtime must stay enterprise-generic. |
| R07 .NET Delivery Driver Isolation | `requirements/01-normalized-requirements.md` | `subbundles/04-dotnet-delivery-driver-isolation` | .NET policy tests and project-reference audit | Must avoid repeating the reverted fix. |
| R08 Template Hardening | `requirements/01-normalized-requirements.md` | `subbundles/05-template-and-process-hardening` | Template parser/audit tests | Must not overfit Calculator/Tetris or force browser proof on non-UI steps. |
| R09 Regression Suite | `requirements/01-normalized-requirements.md` | `subbundles/06-e2e-replay-and-regression-suite` | Unit, integration, process API, and Playwright proof where declared | Closure phase. |
| Latest run blocked at .NET setup/implementation | `analysis/01-current-state.md` | `subbundles/01-runtime-diagnostics-lineage` | Characterization fixture seeded from run shape | Treat latest run as contaminated by reverted patch. |
| Need management-only suppression with dev-capable agent | `inputs/02-structured-input.md` | `subbundles/02-capability-readiness-policy-model` | Suppression readiness test | Must not mutate global agent settings. |
| Need manager fallback distinction | `inputs/02-structured-input.md` | `subbundles/03-driver-owned-recovery-classification` | Recovery classification tests | No silent fallback. |
| Need generic process runtime | `architecture/02-csharp-dependency-direction.md` | `subbundles/04-dotnet-delivery-driver-isolation` | Domain leak architecture test | Applies to runtime, dispatcher, projection, and common MAF workspace prompt normalization. |
