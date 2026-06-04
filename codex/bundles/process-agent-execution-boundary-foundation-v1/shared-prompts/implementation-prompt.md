# Implementation Prompt

You are implementing `process-agent-execution-boundary-foundation-v1`.

Work only one subbundle at a time. Do not jump ahead to Process Core extraction or driver packs.

Hard constraints:

- Preserve MAF product-tool decoupling.
- Do not reintroduce MAF references to Processes, Projects, or Workbench.
- Do not rename process runtime tools.
- Do not weaken read/write access policy.
- Do not move EF entities.
- Do not perform small/medium/mobile UI validation.
- If UI proof is unexpectedly required, use large-screen PC only.

Before starting each subbundle:

1. Read the subbundle README.
2. Read `plan/01-phase-plan.md`.
3. Read the relevant inventories.
4. Run the listed entry scans/tests.
5. Implement the smallest complete change.
6. Record proof under that subbundle's proof folder.
7. Do not proceed past a refactor gate until the gate checklist passes.
