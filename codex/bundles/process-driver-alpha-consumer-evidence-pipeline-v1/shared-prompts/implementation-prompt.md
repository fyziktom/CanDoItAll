# Implementation Prompt

You are implementing `process-driver-alpha-consumer-evidence-pipeline-v1` on branch `maf-processes-refactor`.

Rules:
- Read the latest source before changing files.
- Implement phase by phase.
- Do not create generic driver runtime, registry, selector, DI registration, manager command, workflow hook, scheduler hook, shell execution, Graph/Office call, workspace/storage write, process mutation, claim/transition/finalizer/retry mutation.
- Do not move broad runtime behavior into Core.
- Keep `.NET/Rust transcript verifier` verification-only.
- Critical gates must pass before downstream phases.
- Every code comment must be in English.
- Update proof transcripts and execution report as each phase closes.
