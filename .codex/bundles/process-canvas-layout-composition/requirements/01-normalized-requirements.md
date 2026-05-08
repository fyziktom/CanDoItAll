# Normalized Requirements

| Requirement | Source | Owner | Acceptance |
| --- | --- | --- | --- |
| `REQ-001` | `N001`, `N002`, `N003` | `02-definition-recomposition-tuning` | Recomposition uses dependency-aware layered placement instead of only collision relief. |
| `REQ-002` | `N004` | `02-definition-recomposition-tuning` | Default-route and non-branch structural dependencies stay on the main lane where possible. |
| `REQ-003` | `N005` | `02-definition-recomposition-tuning` | Role nodes are positioned near the average X/Y of related step bindings or decision authority rather than a single global left column. |
| `REQ-004` | `N006` | `02-definition-recomposition-tuning` | Automatic recomposition increases step column and lane spacing enough to reduce ambiguous connection paths. |
| `REQ-005` | `N007` | `02-definition-recomposition-tuning` | Existing canvas UI, manual movement, persistence, and branch semantics remain unchanged. |
| `REQ-006` | `N001`-`N007` | `03-validation-and-browser-proof` | Component tests and browser-visible proof support closure, or an explicit browser validation blocker is recorded. |
| `REQ-007` | `N008`, `N009` | `04-role-instance-composition-and-default-template-repair` | Definition canvas can render multiple visual role nodes for the same role contract, with assignment and decision links routed to the nearest per-step role instance. |
| `REQ-008` | `N010` | `04-role-instance-composition-and-default-template-repair` | Default process templates receive clearer saved canvas coordinates from the current recomposition rules, so newly synced/default processes start from the improved layout. |
