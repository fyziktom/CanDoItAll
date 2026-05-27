# SB16: 16-generic-process-template-and-agent-training-regression

## Goal

Protect generic Processes behavior.

## Required work

- Run non-software templates: customer onboarding, business plan, incident response, architecture decision.
- Add/check an agent-training or agent-improvement process template scenario.
- Ensure artifact validation statuses are not software-specific.
- Ensure workflows remain under Processes.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB16` are filled and the downstream dependency is safe.
