# Structured Input

## Objective

Expand artifact write coordination across storage-backed and record-only projection paths while keeping all source selection and projection semantics stable.

## Hard Constraints

- No Process Core extraction.
- No driver-pack work.
- No large rewrite of `ProcessRunAutomationDispatchService`.
- No public process tool rename/removal.
- No weakening of artifact trust/status/lineage behavior.
- No small/medium/mobile viewport validation.

## Desired Output

A set of small dispatcher isolation changes that make write side effects reusable and testable without hiding source-specific behavior.
