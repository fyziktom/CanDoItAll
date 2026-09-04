# SB01 — Freeze baseline and characterize Agents seams

**Status:** Ready  
**Outcome:** Current source, branch relation, dependencies, CodeAnalytics evidence, test
classification, and exact discovery are frozen before implementation.

## Owned requirements

R-001–R-005, R-010–R-013, R-050–R-055 baseline portion.

## Prerequisites and reopen triggers

- target branch contains shared base v1;
- fetch `components-decoupling` and `development`;
- reopen if either head or target tests move after proof.

## Work

1. Record current SHAs, dirty state, sibling roots/SHAs, SDK, and branch relation.
2. Re-read current target source and tests; update inventories for material drift.
3. Capture CodeAnalytics snapshot/findings/dependency cycle evidence when available.
4. Build AgentFramework and run baseline discovery/execution:
   - route filter expected 10;
   - primary component filter expected 46.
5. Locate and classify every private/source-shape assertion in target tests and the
   adjacent Workflows navigation case.
6. Freeze exact planned new seam test method names/case counts before SB02.
7. Produce no product-code change.

## C# Architecture Impact

Characterization only. It selects the accepted dependency/state baseline.

## Boundary Ownership

No ownership changes in this subbundle.

## Dependency Direction

Record current direction and cycle evidence; do not modify references.

## Pattern Decision

Confirm PSR-01 through PSR-06 still fit current source. Any mismatch repairs the bundle.

## Testability Contract

Baseline tests must be green. Unrelated failure blocks progression; do not “fix while
here” without a separate owner decision.

## Partial Class Policy

Record existing framework Razor partials. Add none.

## Architecture Proof Required

- refreshed source register;
- exact discovery and pass/fail transcripts;
- CodeAnalytics evidence or explicit gap;
- completed component assessments;
- approved progression decision.

## Non-goals

No production/test refactor, no broad stable gate, no browser proof.

## Progression gate

Proceed only when baseline is current, focused tests pass, and no material source drift
invalidates target contracts.
