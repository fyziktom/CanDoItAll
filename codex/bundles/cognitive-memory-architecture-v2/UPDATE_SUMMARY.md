# Architecture Update Summary

## Scope

This refreshed bundle adds Interactive Memory Probing to the Cognitive Memory architecture and aligns the plan with the supplied current code snapshot.

## Current Code Delta

The supplied current code already contains the two prerequisite boundaries that were previously only proposed:

- MAF context contribution boundary through contributor/policy/result contracts and runtime consumption.
- Source snapshot/evidence provider contracts for Workbench, Process runtime evidence, and Workflow runtime evidence.

Therefore this bundle treats those parts as target-branch validation items and focuses on consuming them.

## Added Architecture

- `architecture/15-interactive-memory-probing.md` defines the Dialogue Workbench, probe lifecycle, feedback model, and safe correction rules.
- `architecture/16-probing-regression-and-calibration-loop.md` defines regression tests, calibration metrics, and learning validation loops created from probe failures.
- `contracts/csharp/InteractiveMemoryProbingContracts.cs` defines the service contracts, probe records, findings, feedback actions, and regression test records.
- `subbundles/13-interactive-memory-probing-workbench/README.md` is the executable implementation workstream for Codex.
- `validation/probing-test-matrix.md` defines functional and non-happy-path proof obligations.

## Safety Rule

Probe feedback is evidence, not direct truth mutation. User corrections create review items, correction candidates, knowledge-gap evidence, and regression tests. Active memory still changes only through Cognitive Memory authority services and policy gates.

## Recommended Next Implementation Slice

Run `00-prerequisite-boundary-gate` as a quick validation gate against the target branch, then implement the normal Cognitive Memory foundation and recall stack. Implement Interactive Memory Probing before or alongside Epistemic Drive so gap detection can use real dialogue-derived evidence.
