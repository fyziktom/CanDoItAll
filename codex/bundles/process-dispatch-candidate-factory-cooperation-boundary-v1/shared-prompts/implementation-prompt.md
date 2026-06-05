# Implementation Prompt

You are implementing `process-dispatch-candidate-factory-cooperation-boundary-v1`.

Work on branch `maf-processes-refactor`.

Hard rules:
- Do not create Process Core.
- Do not create production process driver APIs or driver packs.
- Keep all new production helpers module-local under `CanDoItAll.Modules.Processes/Automation/Dispatch`.
- Do not hide EF, execution-client, technical-agent bridge, SaveAgentAsync, workflow/subprocess execution, transition execution, or recovery journal writes inside pure helpers.
- Preserve every `DispatchCandidate` field exactly.
- Browser validation is N/A unless UI files change unexpectedly; do not create small/medium/mobile proof.

Before each downstream phase, satisfy the progression gate from that subbundle README.
