# Implementation Agent Prompt

You are refactoring `CanDoItAll.Modules.Processes` on branch `maf-processes-refactor`.

Execute the bundle phase-by-phase. Do not skip subbundles. Do not collapse the execution report. This is a refactor-only bundle.

Hard constraints:
- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce production driver APIs.
- Do not change UI files.
- Do not create small/medium/mobile proof.
- Preserve all existing dispatch behavior.
- Keep route stage order exactly canonical.
- Do not hide side effects inside vague helpers.
- Every route handler extraction must be accompanied by source scans and focused tests.

At every critical gate, stop and run the required proof before downstream work.
