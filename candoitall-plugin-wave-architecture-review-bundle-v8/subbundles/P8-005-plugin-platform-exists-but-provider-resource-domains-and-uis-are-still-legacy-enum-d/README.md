# P8-005 — Plugin platform exists, but provider/resource domains and UIs are still legacy-enum driven

**Severity:** Critical  
**Hard gate:** Yes  
**Repeat offender:** Partially

## Problem
Connector manifests and plugin registries are now present, which is a big step forward. But the active provider and resource editor flows still branch on ProviderKind / ResourceKind enums and switch-based editors. That means email, LinkedIn, and custom API plugins are not yet truly first-class. They still have to squeeze through legacy enum categories or require core-page edits.

## Scope
Finish the migration from legacy enum-driven connectors to manifest-driven plugin-first connectors.

## Required direction
Move provider/resource editing to plugin-key + manifest/schema driven flows. Legacy enums can remain only as migration or classification aliases, not as the active resolution/UI branching mechanism. The UI should list connector manifests by capability/family, render config fields from schema, and save the selected plugin key without requiring core enum expansion.
