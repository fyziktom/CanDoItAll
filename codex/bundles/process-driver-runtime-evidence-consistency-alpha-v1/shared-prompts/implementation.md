# Implementation Prompt

You are Codex implementing `process-driver-runtime-evidence-consistency-alpha-v1` on branch `maf-processes-refactor`.

Work phase by phase. Before changing code, re-read the current branch source listed in `inputs/source-artifacts.md` because the previous user report says Codex crashed and report-only proof is not enough.

Hard boundaries:
- Do not create a generic driver runtime host, registry, selector, DI registration, manager command, scheduler hook, workflow hook, or execution-capable driver.
- Do not run commands from a driver. Transcript/runtme-evidence drivers only inspect supplied payloads.
- Do not mutate process state, workspace, storage, claims, transitions, finalizers, provider repair, or retries.
- Do not add UI/media changes.
- Do not move side-effect behavior into Core.

Every critical gate must include:
- failing-first or adversarial negative proof,
- semantic positive proof,
- source assertions,
- anti-stub audit,
- changed-file hashes,
- build/test transcripts,
- raw-note closure.
