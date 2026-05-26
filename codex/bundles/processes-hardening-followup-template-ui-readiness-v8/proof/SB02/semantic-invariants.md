# SB02 Semantic Invariants

- Invariant ID: SB02-INV-001
- Expected behavior: Public Processes API and MAF process tools expose the same typed runtime governance model, including definition contract mode, allowed operations, operation target scope, artifact workflow mapping fields, subprocess child mapping fields, run health, recovery recommendations, block reason codes, projection lineage, and projection identity hash.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production API/tool paths; preserving values only in in-memory test setup; exposing HTTP fields while leaving MAF run detail thinner.
- Required proof: adversarial MAF parity proof, passing API integration tests that save/read/export/import typed contract fields and read nested runtime health, source assertions, anti-stub audit, changed-file hashes.
