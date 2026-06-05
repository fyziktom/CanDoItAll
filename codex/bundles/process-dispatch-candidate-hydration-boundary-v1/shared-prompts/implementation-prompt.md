# Implementation Prompt

You are implementing `process-dispatch-candidate-hydration-boundary-v1` on branch `maf-processes-refactor`.

Work subbundle by subbundle. Do not skip progression gates. Do not create Process Core, driver packs, driver registry, or production driver APIs. Keep all new runtime helpers module-local in `CanDoItAll.Modules.Processes`.

Preserve existing behavior. Candidate selection/hydration is safety-critical: route order, durable claim behavior, technical-agent binding, recovery execution selection, and artifact-input prompt shaping must remain semantically identical.

Browser validation is N/A unless UI changes unexpectedly. Do not create small/medium/mobile proof artifacts.
