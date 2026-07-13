# SB04 Semantic Invariants

## Final Closure Contract

- Invariant ID: `CapabilityTemplateSeedMaterializationTests`
- Source raw note: final validation and closure for the full request.
- Expected behavior: The bundle closes only after focused tests, diff checks, proof manifests, raw note closure, and prepared/completed validators provide artifact-backed evidence for the changed git wrapper, agent tools, and skill templates.
- Disallowed shallow implementation: Marking the bundle complete with pending gate rows, missing proof manifests, no validation transcripts, or unresolved raw notes would not satisfy closure.
- Failing-first test: N/A - process/non-production closure proof; SB04 validates evidence and does not introduce production behavior.
- Passing test: `bundle://proof/SB04/transcripts/final-focused-tests.txt` passed the combined focused unit test set, including `CapabilityTemplateSeedMaterializationTests`.
- Changed source files: N/A - process/non-production closure proof; SB04 changed proof artifacts and validation records rather than production code.
- Production assertions: `bundle://proof/SB04/manifest.md` and `bundle://reviews/01-execution-report.md` map requirements and raw notes to concrete SB01-SB03 source/test proof.
- Red-team negative case: SB04 records the existing `CanDoItAll.Web (10824)` file-lock blocker for broad graph tests instead of hiding it behind a silent fallback.
- Downstream dependency check: SB04 re-runs wrapper, runtime, access, MAF composition, template materialization, seed integration, diff check, and bundle validators.
