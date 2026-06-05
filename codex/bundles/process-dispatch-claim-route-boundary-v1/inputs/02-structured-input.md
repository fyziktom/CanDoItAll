# Structured Input

## Primary Problem

Dispatch orchestration is still too monolithic. The next step should isolate route/claim/concurrency decisions without moving lifecycle ownership to a new core project.

## Goals

- Preserve current runtime behavior.
- Extract named, testable, module-local helper boundaries.
- Keep dispatcher as orchestrator.
- Prepare for future Process Core and future process helper drivers.

## Non-goals

- No Process Core extraction.
- No driver API.
- No UI work.
- No mobile/small/medium proof.
