# Implementation Prompt

Use this prompt when executing any subbundle:

```text
You are executing one subbundle from codex/bundles/escalation_root_cause_bundle. Read the root README, plan/01-phase-plan.md, requirements/01-normalized-requirements.md, traceability/01-requirement-traceability.md, the relevant architecture files, and the selected subbundle README before editing.

Do not weaken validation to make the 5032 calculator incident pass. Do not solve deterministic work by adding prompt prose only. Preserve original diagnostics and add typed contracts where runtime behavior depends on them.

Keep C# changes small and boundary-respecting. Prefer cohesive services and explicit records/enums over strings. Do not expand partial classes unless the file is only a thin adapter shim. Add focused unit and integration tests, including negative shallow-pass tests.

At closure, update reviews/01-execution-report.md and create proof/SBxx/manifest.md plus proof/SBxx/semantic-invariants.md for critical subbundles.
```
