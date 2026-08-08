# Claude Fable 5 execution profile

## Intended use

Use Fable 5 for the implementation subbundles because they require sustained repository analysis, multi-file refactoring, test repair, and architecture proof. The bundle does not depend on a private model capability or an exact API model identifier.

## Reasoning setting

Use the deepest/maximal reasoning mode available. `xHigh` is the user's intent label and is deliberately not embedded as an assumed Claude CLI flag.

## Operating style

- Prefer exact source evidence over broad speculation.
- Maintain a visible task checklist in durable files.
- Read narrowly by subbundle to preserve context capacity.
- Implement and test; do not stop after producing a plan.
- Re-evaluate the plan when current branch evidence differs from the analyzed baseline.
- Use explicit negative tests and source guards to prevent shallow refactors.

## Fallback

Prefer Claude Opus 5 only when that model is actually configured and available in the operator environment. Otherwise select the best available high-capability Claude model. The bundle does not assume an exact fallback model ID.

A fallback Claude model must read the same subbundle prompt, README, proof manifest, and session handoff. It must not silently reduce architecture gates or reinterpret unresolved authority/persistence decisions.
