# SB02: MAF 1.6 Feature Adoption Matrix

## Status

- Completed

## Objective

Create a concrete MAF 1.6 feature adoption matrix with adopted, deferred, not applicable, and blocked decisions.

## Covered Inputs

- RQ02: create a MAF 1.6 feature adoption matrix.
- RQ03: decide which useful MAF 1.6 features reduce process failures.

## Prerequisites

- SB01 package baseline must be complete.

## Exact Source References

- bundle://analysis/02-maf16-official-notes.md
- repo://src/CanDoItAll.AgentFramework.Maf/README.md
- repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj

## Deliverables

- Adoption matrix covering `IChatMessageInjector`, `AgentSessionFiles`/file store, stream-error input persistence, tool approval/middleware, local/hosted MCP metadata, workflow evaluation expected output, A2A v1, OpenTelemetry auto-wiring, and skills/frontmatter.
- Code/test links for every adopted feature.

## Dependency Impact

- SB03 through SB08 depend on this decision matrix and must not invent feature scope independently.

## Validation Depth

- Use official Microsoft/NuGet sources and local source assertions.
- Critical semantic proof must reject a docs-only matrix with no code/test mapping.

## Implementation Steps

- Verify official MAF 1.6 sources.
- Update the adoption matrix and classify each feature.
- Link adopted items to implementation and tests.
- Update `proof/SB02`.

## Do Not Do

- Do not label a feature adopted unless a production path or explicit compatibility adapter exists.
- Do not hide deferred or blocked features.

## Acceptance Checklist

- Matrix covers every required feature.
- Each adopted/deferred/blocked decision has rationale and proof.
- Downstream subbundles have clear implementation boundaries.

## Proof Required

- Official-source transcript or cited artifact.
- Source assertions and anti-stub audit.
- Changed-file hashes and semantic invariant proof in `bundle://proof/SB02`.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB03 through SB08 may start only after the adoption matrix lists the exact feature decisions they own.

## Suggested Agent Prompt

Build the MAF 1.6 adoption matrix from official sources and local source references, then map every adopted or deferred feature to downstream implementation proof.

## Closure Proof

- bundle://proof/SB02/manifest.md
- bundle://proof/SB02/semantic-invariants.md

