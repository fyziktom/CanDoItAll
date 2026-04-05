# ADR-0004: Workbench Node Extension Guardrails

## Status

Accepted

## Context

The universal Workbench node is still broad and shared by many feature areas. A rushed family split would be high-risk, but continuing to extend the node without constraints would recreate the same canonical-model problems in new forms.

## Decision

- This wave does not split the universal Workbench node into separate storage families.
- New Workbench node capabilities must declare which data is canonical, which data is projection-only, and which lifecycle owner enforces invariants before implementation starts.
- When a proposed feature needs reusable identity, cross-module ownership, or independent lifecycle rules, the default answer is a separate canonical model plus projection into Workbench, not more metadata on the node.

## Consequences

- Feature design reviews should reject new metadata fields that act like hidden canonical stores.
- Workbench can continue to evolve for presentation, grouping, and local workflow state, but reusable business identity should live outside it.
- If repeated feature pressure keeps adding canonical behavior into Workbench, that becomes the trigger for a later typed-family or capability-contract redesign.
