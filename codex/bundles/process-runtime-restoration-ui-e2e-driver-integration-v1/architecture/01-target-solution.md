# Target Architecture

## Desired system shape

The system should have three clearly separated layers:

1. **Process Core**  
   Deterministic read models and pure rules only. No runtime orchestration, no UI, no storage, no workspace, no external calls, no drivers.

2. **Process Module Runtime**  
   Owns process templates, UI-visible process start, process runs, dispatch, claims, transitions, artifacts, finalizers, MAF/workflow/direct-agent execution, scheduler integration, persistence, and app composition.

3. **Domain Driver Layer**  
   Read-only verification helpers and future domain helpers. Current allowed drivers are verification-only over supplied evidence. They can return diagnostics/audit/redaction/no-mutation envelopes. They must not mutate process state or execute commands.

## Immediate target of this bundle

The immediate target is not to expand Process Core. The immediate target is to restore and prove process runtime usability:

- app starts,
- UI process selection works,
- run creation works,
- dispatch advances runs,
- `.NET app` and business-analysis scenarios execute through the process machinery,
- read-only driver verification remains bounded and useful.

## Runtime host decision

A production generic driver runtime host remains **not approved** in this bundle.

A future host can be proposed only after:
- process runtime is working from UI again,
- verification-only adapter integration is proven,
- no-mutation/audit/redaction are stable,
- user-visible process scenarios pass,
- lifecycle ownership is explicit.
