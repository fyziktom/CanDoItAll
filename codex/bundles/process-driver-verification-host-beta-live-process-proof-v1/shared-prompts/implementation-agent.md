# Implementation Agent Prompt

You are implementing `process-driver-verification-host-beta-live-process-proof-v1`.

Rules:
- Re-read source before every phase.
- Do not trust previous execution report rows as proof.
- Do not introduce execution-capable drivers.
- Do not log secrets.
- Do not mutate process state from driver host paths.
- Do not add Process Core references to drivers/modules/infrastructure.
- Use source-backed tests and transcript artifacts for every critical gate.
- Replace collapsed execution rows with separate SB001-SB066 rows as work progresses.
