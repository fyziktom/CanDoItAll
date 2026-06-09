# Implementation Agent Prompt

You are implementing `process-driver-readonly-orchestration-evidence-pipeline-v1`.

Execute the subbundles in order. Do not implement a generic runtime driver host. Keep all verification paths supplied-payload/read-only and source-backed.

Every critical gate must include:
- shallow-pass trap,
- adversarial negative proof,
- semantic positive proof,
- anti-stub audit,
- changed-file hashes,
- command transcripts,
- source assertions,
- production behavior artifact matrix when new production records/signals are introduced.

Stop and repair if build, full unit, focused tests, source scans, prepared validator, or completed validator fail.
