# Phase Plan

## Phase Sequence

1. Normalize the Zyphonote findings and freeze the exact parity scope for this pass.
2. Implement direct project and solution navigation parity in the sibling repo and host MCP.
3. Implement member/source inspection parity and fix or bypass the failing member-focused summary path.
4. Extend reinstall and Codex skill guidance so the new tools are actually consumable.
5. Reinstall, request a Codex restart if needed, rerun the five Zyphonote scenarios, and close the bundle with recorded findings.

## Subbundle Dependency Map

```mermaid
gantt
title CodeAnalytics Zyphonote parity execution map
dateFormat  YYYY-MM-DD
section Foundations
SB-01 Findings and gap inventory :done, sb01, 2026-04-08, 1d
SB-02 Project and solution navigation parity :after sb01, sb02, 1d
SB-03 Member behavior and source inspection parity :after sb02, sb03, 1d
section Host rollout
SB-04 Host integration and skill guidance :after sb03, sb04, 1d
section Closure
SB-05 Zyphonote rerun and closure :after sb04, sb05, 1d
```

- SB-02 cannot start until the parity inventory is frozen, because it defines which missing surfaces are in scope.
- SB-03 depends on SB-02 because scenario 4 proof is only meaningful after the new lower-level navigation path exists.
- SB-04 depends on SB-02 and SB-03 because reinstall and skill guidance must reflect the actual new tool surface.
- SB-05 depends on all prior subbundles and may require a user-assisted Codex restart before final proof can start.

## Critical Subbundles

- `SB-02 Project and solution navigation parity`
- Require build success plus at least one targeted validation showing direct project references are returned cleanly.
- `SB-03 Member behavior and source inspection parity`
- Require build success plus targeted validation that a realistic member-behavior query no longer fails and produces usable evidence.

## Phase Gates

- Gate after preparation: run `validate_bundle.py --stage prepared` and repair any missing structure or weak source references.
- Gate before each subbundle: confirm all prerequisite subbundles are complete and their proof still reflects the current code.
- Gate after SB-02: require build success and direct project-reference validation before SB-03 starts.
- Gate after SB-03: require build success and member-behavior validation before SB-04 starts.
- Gate after SB-04: require reinstall success and confirm whether a Codex restart is now mandatory for final proof.
- Gate before closure: rerun validators, rerun the five Zyphonote scenarios, update findings, and reopen any subbundle whose proof did not hold.
