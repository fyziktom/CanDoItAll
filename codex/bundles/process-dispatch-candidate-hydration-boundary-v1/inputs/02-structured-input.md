# Structured Input

## Primary Goal

Prepare the next safe refactor bundle that continues decomposing process dispatch services after the claim/route boundary work.

## Recommended Scope

The next seam is **candidate selection and candidate hydration**. It should remain module-local and should not create `CanDoItAll.Processes.Core` or production process-driver APIs.

## Hard Non-Goals

- No Process Core project or namespace.
- No process driver packs, driver registry, driver interfaces, or public driver contract.
- No EF write movement into pure helpers.
- No workflow/subprocess/execution-client/finalizer side-effect movement into pure helpers.
- No public process tool renames or access-policy weakening.
- No UI changes and no small/medium/mobile proof.

## Expected End State

- Candidate header selection is isolated behind a module-local selector/query helper.
- Candidate hydration is split into data-loading, snapshot assembly, artifact-input shaping, assignment/workflow recognition, and technical-agent binding preparation helpers.
- Dispatcher still owns lifecycle routing and side effects, but the long inline hydration method becomes smaller and easier to test.
- Driver readiness is documented as candidate/evidence intent mapping only; no production API yet.
