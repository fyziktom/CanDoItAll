# Implementation Agent Prompt

You are implementing `process-runtime-live-e2e-openai-hardening-v1`.

Read `inputs/`, `analysis/`, `architecture/`, and every subbundle README before editing. Work phase by phase. Do not skip critical gates. Do not implement a generic driver runtime host. Do not add long-lived test dependencies on concrete bundle folders.

For each critical subbundle:
- run the required source scans and tests,
- write `proof/SBxx/manifest.md`,
- write `proof/SBxx/semantic-invariants.md`,
- capture failing-first/adversarial proof and semantic positive proof,
- update `reviews/01-execution-report.md`.

OpenAI live proof is opt-in only. If the flag/key is absent, record an explicit skip with deterministic proof still passing.
