# Structured Input

## Core Objective

- Preserve the real failed run evidence from `http://localhost:5032`.
- Make the failure reproducible from API records without relying on user memory.
- Give ChatGPT Pro enough detail to prepare the actual repair bundle.

## Success Criteria

- Raw API payloads are saved under `inputs/api-evidence/`.
- The failed run, failed step, artifact record, execution runs, pending approval, definition, launch-plan, and project-structure context are indexed.
- This package is explicitly marked input-only.

## Hard Constraints

- Do not implement code changes in this input package.
- Do not select or propose a repair implementation in this input package.
- Do not mutate the failed process run, transitions, artifacts, approvals, or project structure.
- Preserve raw API payloads alongside summaries.

## Allowed Side Effects

- Create input files under this bundle only.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `inputs/03-api-evidence-index.md`
- `inputs/04-chatgpt-pro-handoff.md`
- `inputs/api-evidence/`

## Input Coverage Signals

- Process run id `9bbc0667-9d12-4506-ba81-654ef924cad6`
- Failed step run id `0610f6d6-5d37-4313-b560-09cc9484f5b8`
- Artifact record id `aa9a3e75-8d3e-4757-bafa-be00e8678b8d`
- Step execution run id `91e6a078-ac63-43e6-9901-6f8364539c42`
- Later manager-chat execution run id `d38da822-a980-44ce-952b-6e86c0b74bbb`
- Open escalation id `e408fdcf-bdf8-4988-87e3-43a60b920f7d`

## Dependency And Sequencing Signals

- Step 1 depends on step 0 completing through its default branch.
- No downstream process steps ran because step 0 failed.
- A pending manager-chat approval may change runtime state if decided after this capture.

## Validation Expectations

- Validate this input package by inspecting the raw API files and `inputs/03-api-evidence-index.md`.
- Treat `README.md` status `Input-only` as intentional.
- Do not use this bundle as a completed `candoitall-bundle-preparation` artifact.

## Evidence Contract

- Raw API payloads must remain unchanged after capture.
- Any later repair bundle should cite these files instead of relying on prose only.

## UI Validation Strategy

- Not applicable to this input-only package.

## Browser Validation Analytics

- Not applicable to this input-only package.

## Working Assumptions

- The relevant failed run is `9bbc0667-9d12-4506-ba81-654ef924cad6`, because it is the only recent process run returned by the API and it matches the user's description.
- The local API instance at `http://localhost:5032` is the authoritative runtime state for this handoff.
- ChatGPT Pro will create the implementation-ready bundle and execute any source-level diagnosis.

## Primary Risks

- The process API exposes `invariantDiagnosticCount` but not the full diagnostic list in the captured run detail response.
- The managed artifact file content was not directly downloadable by raw path through the tested storage endpoints.
- The later manager-chat execution is active and waiting on a tool approval; its state may change after this input capture.
