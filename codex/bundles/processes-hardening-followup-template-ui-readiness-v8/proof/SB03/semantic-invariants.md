# SB03 Semantic Invariants

- Invariant ID: SB03-INV-001
- Expected behavior: Every process template listed in `repo://Templates/Processes/manifest.json` is represented in a step-level governance matrix that records allowed operations, operation target scope, branch outcomes, required artifact count, artifact input count, exception policy presence, strict-governance readiness, and downstream migration ownership for remaining typed-contract gaps.
- Disallowed shallow implementation: a prose-only inventory that omits manifest entries, a matrix that ignores individual steps, or a pass result that hides typed-contract gaps without assigning a concrete migration subbundle.
- Required proof: strict audit failing-first transcript, successful matrix generation transcript, source assertions, anti-stub audit, changed-file hashes, and `bundle://proof/SB03/template-governance-matrix.md`.
