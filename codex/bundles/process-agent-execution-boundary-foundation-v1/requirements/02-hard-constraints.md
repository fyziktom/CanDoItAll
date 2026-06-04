# Hard Constraints

- Keep previous MAF/tool-provider decoupling intact.
- Do not reintroduce MAF references to `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.Projects`, or `CanDoItAll.Modules.Workbench`.
- Do not rename or remove public process runtime tools.
- Do not weaken process read/write access checks.
- Do not start domain driver-pack work.
- Do not move EF entities.
- Do not perform a broad dispatcher rewrite.
- Do not perform mobile, small-screen, or medium-screen UI validation.
- If browser proof is unexpectedly needed, use large-screen PC viewport only.
- Every production movement subbundle must include source assertions, tests, and a progression gate.
