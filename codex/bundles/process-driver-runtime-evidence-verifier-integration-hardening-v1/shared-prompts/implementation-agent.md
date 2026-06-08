# Implementation Agent Prompt

You are implementing `process-driver-runtime-evidence-verifier-integration-hardening-v1`.

Work phase by phase. Do not skip critical gates. Do not introduce runtime driver registry/selector/DI/manager command/scheduler/workflow hook. Do not read files or call external services from verifier packages. Consume supplied payloads only.

For every critical gate:
- run required tests,
- capture transcripts,
- update proof/SBxx/manifest.md,
- include changed-file hashes,
- include shallow-pass trap, adversarial negative proof, semantic positive proof and anti-stub audit,
- update reviews/01-execution-report.md with separate rows.

Stop immediately if a change requires process mutation, workspace/storage writes, shell execution, Graph/Office calls, claim/transition/finalizer/retry ownership, or broad Core runtime extraction.
