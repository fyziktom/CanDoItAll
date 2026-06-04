# Implementation Prompt

You are implementing `process-dispatch-artifact-write-coordinator-expansion-v1` in `fyziktom/CanDoItAll`, branch `maf-processes-refactor`.

Rules:

- Do not create Process Core or driver-pack projects.
- Do not move EF entities, Razor UI, storage implementations, or MAF/Tooling code.
- Keep comments in code in English.
- Preserve existing behavior exactly unless a subbundle explicitly says otherwise.
- Migrate one artifact write path at a time.
- Run the gate proof before starting downstream subbundles.
- Do not run small/medium/mobile viewport proof.

Start each subbundle by reading its README, the phase plan, and the source artifacts list. Record proof under the corresponding `proof/SBxx` folder.
