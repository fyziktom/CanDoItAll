# Implementation Prompt

Execute the current subbundle only. Keep all changes module-local under `CanDoItAll.Modules.Processes`, preserve runtime behavior, and do not create Process Core, production driver APIs, driver registries, or driver packages.

Before editing production code, pass the subbundle entry gate by reading the root README, phase plan, traceability, raw inputs, and the selected subbundle README. After implementation, update the subbundle status, `reviews/01-execution-report.md`, and required proof artifacts before moving downstream.

For critical subbundles, create `bundle://proof/SBxx/manifest.md` and `bundle://proof/SBxx/semantic-invariants.md` with changed-file hashes, transcripts, source assertions, anti-stub audit output, shallow-pass trap, adversarial negative proof, semantic positive proof, and raw-note closure.
