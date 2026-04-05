# Structured Input

## Problem Statement

- The codebase still allows node-scoped party ownership to drift between workbench metadata and CRM/HR assignment rows.
- Workbench node delete and subtree transfer do not currently reconcile canonical assignment rows.
- The existing `v2` bundle is not validator-compatible, so execution cannot be honestly closed without a normalized repair bundle.

## Execution Goal

- Repair only the canonical seam that creates future refactor risk:
  - canonical read/write path for node-scoped party ownership
  - canonical lifecycle reconciliation for delete and subtree move
  - targeted tests and browser proof for those flows
  - post-fix architecture review using the integrated skillset

## Non-Goals

- Do not solve the broader universal-node refactor in this bundle.
- Do not replace the current `NodeKey` contract with a new typed identifier system in this bundle.
- Do not widen CRM/HR feature scope beyond what is required to remove the split source of truth and lifecycle drift.

## Required Proof

- Prepared-stage bundle validation must pass before production edits are considered in-scope.
- Relevant builds and targeted test slices must pass.
- Playwright browser validation must be attempted through MCP first and must produce screenshots if the route is working.
- The final execution report must include a post-fix architecture analysis and updated residual risks.
