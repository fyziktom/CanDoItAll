# Implementation Prompt

Execute the selected subbundle from `process-core-contract-candidate-driver-readiness-prep-v1`.

Rules:
- Treat the subbundle README as the contract.
- Preserve runtime behavior.
- Keep changes inside the process module and existing test projects unless the subbundle explicitly says otherwise.
- Do not create Process Core or production driver APIs.
- Do not change UI, Razor, CSS, JS, TS, or media files unless source inspection proves the bundle is wrong; if that happens, repair the bundle first.
- Record proof under `bundle://proof/SBxx/` and update `reviews/01-execution-report.md`.
- For critical gates, create `manifest.md`, `semantic-invariants.md`, transcripts, hashes, source assertions, anti-stub audit output, and required passing/failing proof before closure.
