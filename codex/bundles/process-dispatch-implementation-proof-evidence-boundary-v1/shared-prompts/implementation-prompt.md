# Suggested Implementation Prompt

You are working in `fyziktom/CanDoItAll` on branch `maf-processes-refactor`.

Execute this bundle in strict subbundle order. Do not skip gates. Do not create Process Core, production driver APIs, driver packs, UI changes, or small/medium/mobile proof artifacts.

Every production helper must be module-local under:
`src/CanDoItAll.Modules.Processes/Automation/Dispatch/`

Preserve existing wrapper method names unless the subbundle explicitly allows removing them. This is a behavior-preserving refactor.

Before each critical gate:
1. run focused tests,
2. run source scans,
3. update proof manifest,
4. update execution report,
5. decide continue/reopen.

If a helper extraction changes a summary string, status decision, retry decision, or missing tool list, stop and repair.
