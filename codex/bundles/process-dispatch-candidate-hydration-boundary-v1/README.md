# Process Dispatch Candidate Hydration Boundary v1

Status: Prepared  
Created: 2026-06-05 12:10:07Z  
Target branch: `maf-processes-refactor`  
Profile: `initiative`

## Purpose

Continue the gradual decomposition of the process dispatcher without introducing `CanDoItAll.Processes.Core` and without creating production process-driver APIs.

The previous claim/route bundle established useful route and concurrency boundaries. The next safe seam is candidate header selection and candidate hydration, which still combine a large amount of EF readback, shaping, assignment resolution, technical-agent binding, project-structure access mutation, recovery selection, and candidate construction.

## Scope

This bundle prepares and executes a module-local candidate hydration boundary:

- candidate header selector,
- candidate hydration snapshot/loader,
- artifact-input assembler,
- branch/dependency context helper,
- assignment/workflow route helper,
- technical-agent binding/access coordinator,
- manual recovery/recoverable execution query helper,
- documentation-only candidate/evidence driver-readiness map.

## Non-Goals

- No Process Core.
- No production process driver API, driver pack, registry, or DI registration.
- No public process contract promotion.
- No UI changes.
- No small/medium/mobile proof.

## Subbundles

- SB01: Entry audit, branch hygiene, previous claim/route boundary smoke
- SB02: Live inventory of candidate header selection and hydration
- SB03: Design local selector/loader/assembler/coordinator seams
- SB04: Gate A guardrails before production movement
- SB05: Introduce module-local candidate header selector
- SB06: Migrate LoadDispatchCandidateHeadersAsync through selector
- SB07: Introduce read snapshot records and loader cutline
- SB08: Gate B candidate header/snapshot parity
- SB09: Move artifact-input prompt shaping behind local helper
- SB10: Move branch outcome/conditional dependency shaping
- SB11: Move current assignment/workflow route recognition
- SB12: Gate C candidate assembly parity
- SB13: Introduce side-effect-explicit technical-agent binding coordinator
- SB14: Use binding coordinator in direct-agent hydration
- SB15: Move manual recovery/recoverable execution query helpers
- SB16: Gate D runtime smoke, line counts, source scans
- SB17: Documentation-only driver readiness candidate/evidence map
- SB18: Final red-team and next safe cutline

## Validation Summary

Prepared bundle. Codex must fill execution proof during implementation.
