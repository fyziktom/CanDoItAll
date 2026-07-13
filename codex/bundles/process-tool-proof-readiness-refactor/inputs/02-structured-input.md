# Structured Input

## Problem Statement

Process steps currently express important tool and proof requirements mostly through prose and broad allowed operations. In run `6f0d229f`, the QA recheck step had access to Playwright and workspace tools, but the process did not enforce a typed requirement that those tools must produce current-run receipts before accepting the step outcome. This allowed artifact-only repair attempts and recovery finalization to loop without collecting the actual browser/image proof.

## Desired Behavior

- Process definitions and drivers declare a typed step contract for:
  - allowed capabilities,
  - denied or suppressed capabilities,
  - required capabilities,
  - required runtime tool receipts,
  - required MCP tool receipts,
  - scoped instruction fragments.
- HR/project-structure role matching evaluates that contract before process launch or dispatch.
- Runtime execution metadata carries the contract into MAF without leaking domain-specific instructions into common workspace tools.
- Step outcome finalization rejects or escalates outcomes that claim success while required current-run receipts are missing.
- Manager fallback uses a typed missing-proof diagnosis to reassign, re-dispatch with a proof-focused strategy, or invoke a process driver.

## Out Of Scope For This Bundle Preparation

- Product implementation.
- Rebuilding the 5032 instance.
- Re-running the process.
- Removing project artifacts.

## Architecture Constraints

- Keep MAF generic. MAF may enforce generic capability policies and receipt gates, but it must not know that software delivery QA needs UI screenshots.
- Keep domain-specific process requirements in process templates, process drivers, or process-owned instruction fragments.
- Do not create a broad service locator or stringly typed tool policy surface.
- Prefer immutable records, cached compiled contracts, and explicit interfaces only where they define a real boundary.
