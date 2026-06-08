# Structured Input

## User Objective
Review the latest Codex result after crash, verify real code, identify missing repairs/improvements, and prepare a next implementation-ready bundle that moves toward a complete stable Process Core with domain drivers.

## Current Verified Direction
The system now has multiple read-only alpha driver packages. The next useful work is to make domain lanes safely consumable through explicit gateway/adapters and remove remaining proof/test debt, not to introduce a generic runtime driver host.

## Hard Constraints
- Preserve existing runtime behavior.
- Keep Process Core deterministic and dependency-clean.
- No broad Process Core runtime extraction.
- No generic driver runtime registry, selector, dependency-injection registration, manager command, scheduler hook, workflow hook, shell execution, Office/Graph calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, or retry scheduling.
- Domain drivers remain verification-only and must inspect supplied evidence only.
- No UI/browser/mobile/small/medium proof unless UI/media files unexpectedly change; such change should fail and force re-scope.
- New production signals, records, or events require producer/consumer/lifecycle/negative-test proof matrices.
