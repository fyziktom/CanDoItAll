# SB02 Finalizer Source Inventory Semantic Invariants

- Invariant ID: SB02-INV-001
- Source raw note: Continue smaller dispatcher isolation steps.
- Expected behavior: The bundle has a source-backed finalizer inventory that identifies the current target surface before code movement.
- Disallowed shallow implementation: A shallow inventory that lists only the filename or omits transition/request dependencies.
- Failing-first test: N/A process/documentation-only; no production behavior changed in this inventory gate.
- Passing test: bundle://proof/SB02/transcripts/source-inventory.txt
- Changed source files: repo://codex/bundles/process-dispatch-step-completion-finalizer-boundary-v1/inventories/01-source-impact-inventory.md; repo://codex/bundles/process-dispatch-step-completion-finalizer-boundary-v1/inventories/02-finalizer-method-classification-template.md; repo://codex/bundles/process-dispatch-step-completion-finalizer-boundary-v1/inventories/04-test-impact-inventory.md
- Production assertions: Processes-module behavior is preserved; no Process Core project, driver pack API, or UI file change is introduced.
- Red-team negative case: bundle://proof/SB02/transcripts/anti-stub-audit.txt rejects placeholder exception/TODO implementation markers and boundary drift for this scope.
- Downstream dependency check: Execution report gate row and final red-team scan confirm downstream SBs can proceed or close without expanding the process-driver boundary.
