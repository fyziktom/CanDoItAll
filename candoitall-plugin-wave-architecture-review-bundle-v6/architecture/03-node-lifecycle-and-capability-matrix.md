# Node Lifecycle and Capability Matrix

## Lifecycle

The node id should stay stable across brainstorming and refinement.

Example:

- user adds a quick note node
- later reclassifies it into a task / decision / connector / other richer block
- the same node survives
- a lifecycle history row records the semantic transition
- the old facet snapshot is preserved or superseded according to the transition rule

## Capability matrix

The node-kind registry should declare at least:

- kind key
- family
- editor schema
- allowed outgoing / incoming relation kinds
- allowed commands
- whether node is assignable
- which assignment roles are allowed
- whether node can own bindings of specific kinds
- transition rules from other families

## Why this matters for CRM/HR

The system already wants to assign people, agents, partners, reviewers, and similar actors to nodes.

That means the system needs a single answer to questions like:

- can this kind accept an AI agent?
- can this kind accept a reviewer?
- can this kind accept a billing contact?
- can this kind accept only one assignee or multiple?

Those answers belong in the capability matrix, not in scattered role-specific special cases.
