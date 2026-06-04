# SB12 Semantic Invariants

## Invariant SB12_INV_001

- Invariant ID: `SB12_INV_001`
- Source raw note: "Perform final red-team review" and "Re-run hidden dependency and direct execution coupling scans."
- Expected behavior: Final scans show no MAF/Tooling product-code dependency, no forbidden Contracts dependency, no dispatcher direct workspace execution coupling, and no UI route changes.
- Disallowed shallow implementation: Closing the bundle from prior subbundle evidence without rerunning final dependency and direct-coupling scans.
- Failing-first test: N/A - SB12 is final review and cutline proof with no production behavior change.
- Passing test: `bundle://proof/SB12/transcripts/hidden-dependency-final-scan.txt`; `bundle://proof/SB12/transcripts/dispatcher-direct-coupling-final-scan.txt`; `bundle://proof/SB12/transcripts/ui-diff-final-scan.txt`.
- Changed source files: SB12 adds proof only.
- Production assertions: `bundle://proof/SB12/source-assertions/final-red-team-next-core-cutline.txt`.
- Red-team negative case: A product dependency in MAF/Tooling, forbidden Contracts dependency, dispatcher direct `workspaceService.` call, or UI file diff blocks final closure.
- Downstream dependency check: No downstream bundle should start until final closure scans pass.

## Invariant SB12_INV_002

- Invariant ID: `SB12_INV_002`
- Source raw note: "Review all requirements in traceability matrix."
- Expected behavior: RQ-001 through RQ-014 all have completed owner subbundles, proof manifests, source assertions, and transcript-backed validation.
- Disallowed shallow implementation: Reporting only final tests while leaving traceability requirements unclosed or unlinked from proof.
- Failing-first test: N/A - SB12 reviews completed proof artifacts.
- Passing test: `bundle://proof/SB12/transcripts/requirement-traceability-review.txt`; `bundle://proof/SB11/transcripts/provider-policy-unit-tests.txt`; `bundle://proof/SB11/transcripts/process-filtered-integration-tests.txt`; `bundle://proof/SB11/transcripts/full-solution-build.txt`.
- Changed source files: SB12 adds proof only.
- Production assertions: `bundle://proof/SB12/source-assertions/final-red-team-next-core-cutline.txt`.
- Red-team negative case: Any missing RQ owner/proof mapping in the traceability matrix or execution report blocks final closure.
- Downstream dependency check: The next bundle can use this report as the baseline for new extraction work.

## Invariant SB12_INV_003

- Invariant ID: `SB12_INV_003`
- Source raw note: "Define next bundle cutline: whether actual Process Core extraction can start" and "Do not run small, medium, or mobile UI validation."
- Expected behavior: Next Process Core extraction is allowed only as a narrow dependency-neutral bundle with fresh guardrails; no EF entities, UI models, provider/runtime implementations, MAF dependencies, tool renames, access-policy weakening, driver packs, or mobile proof are allowed by this bundle.
- Disallowed shallow implementation: Declaring Process Core extraction generally approved without naming allowed and prohibited movement boundaries.
- Failing-first test: N/A - this is the final cutline decision.
- Passing test: `bundle://proof/SB12/transcripts/no-core-driver-project-final-scan.txt`; `bundle://proof/SB12/transcripts/no-forbidden-viewport-proof-path-final-scan.txt`.
- Changed source files: SB12 adds proof only.
- Production assertions: `bundle://proof/SB12/source-assertions/final-red-team-next-core-cutline.txt`.
- Red-team negative case: A new core/driver project in this bundle or a mobile/small/medium proof artifact path fails SB12 scans.
- Downstream dependency check: A future Process Core bundle must start with its own inventory, architecture tests, and dependency-neutral extraction scope.
