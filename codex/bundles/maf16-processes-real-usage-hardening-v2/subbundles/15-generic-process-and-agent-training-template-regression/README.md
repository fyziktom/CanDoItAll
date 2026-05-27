# SB15: 15-generic-process-and-agent-training-template-regression

## Goal

Protect generic process behavior after MAF/process fixes.

## Required work

- Run non-software templates and agent-improvement/training style processes through template/lint tests.
- Ensure artifact validation remains generic and does not assume Blazor/software artifacts.
- Ensure workflow/subprocess bridge works for business artifacts and not only code artifacts.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB15` are updated and downstream subbundles can rely on the behavior.
