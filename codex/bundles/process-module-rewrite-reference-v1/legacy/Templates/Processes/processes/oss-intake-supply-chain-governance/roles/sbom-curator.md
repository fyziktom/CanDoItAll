# SBOM curator

**Key:** `sbom-curator`  
**Scope:** local  
**Process:** oss-intake-supply-chain-governance  
**Preferred executor:** person-or-agent  
**Preferred project role:** TeamMember  
**Seniority:** Senior release engineering or supply-chain operations  
**Minimum years in primary discipline:** 4  
**Minimum years in software delivery:** 6

## Summary
Metadata owner for component inventory, provenance completeness, and reusable dependency evidence.

## Purpose
Keep the software bill of materials and dependency evidence accurate enough for governance, response, and later reuse.

## Staffing intent
A supply-chain operations specialist who can structure component metadata and evidence consistently.

## Snapshot summary
Metadata owner for component inventory, provenance completeness, and reusable dependency evidence.

## Domain tags
sbom, dependency-metadata, provenance, supply-chain

## Knowledge requirements
- Knowledge of SBOM concepts, provenance metadata, component identifiers, and dependency relationship modeling.
- Ability to collect and normalize component data from build, package, and repository sources.
- Understanding of how missing metadata weakens vulnerability and compliance response.
- Knowledge of SPDX-compatible or equivalent metadata structures and reuse expectations.
- Ability to track completeness, gaps, and manual overrides transparently.
- Understanding of retention and sensitivity requirements for supply-chain evidence.

## Experience requirements
- Has built or maintained component inventory or SBOM outputs for a software product.
- Has reconciled incomplete dependency metadata and documented the gaps.
- Has worked with engineering, security, and compliance on dependency evidence needs.
- Has supported vulnerability-response or audit work using component metadata.
- Has improved repeatability of dependency evidence generation.

## Decision rights
- Approve metadata completeness for the OSS intake gate.
- Require missing provenance or component identifiers before release use.
- Escalate when dependency evidence is not reproducible.
- Define reuse restrictions for incomplete or provisional SBOM outputs.

## Owned artifacts
- SBOM manifest
- Provenance mapping note
- Dependency evidence gap log

## Collaboration expectations
- Coordinate with platform, security, and license roles.
- Keep evidence structured, versioned, and reviewable.
- Document manual assumptions instead of burying them.
- Support rapid lookup during vulnerability or audit events.

## Anti-patterns
- Publishing an SBOM that looks complete but contains unexplained blind spots.
- Treating dependency metadata as a one-time release artifact instead of a reusable capability.
- Ignoring transitive or generated dependencies that materially affect risk.
- Allowing manual corrections without provenance.

## Fitness evidence
- Structured SBOM outputs used in later reviews or incidents.
- Traceable reconciliation notes for incomplete metadata.
- Reduced dependency-visibility gaps over time.
- Stakeholder trust in the curator’s completeness signals.
