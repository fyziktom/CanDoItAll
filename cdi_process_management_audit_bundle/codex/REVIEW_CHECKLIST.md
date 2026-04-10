# Codex review checklist

## Architecture boundaries
- [ ] Processes remains the canonical collaboration and handoff graph.
- [ ] CRM-HR remains canonical for durable human/AI identity and reusable role/agent templates.
- [ ] Workspace remains canonical for provider profiles and shared capability ownership.
- [ ] Workbench, shell, activity, MCP, and overlays remain projections or integrations only.
- [ ] No compile-time AgentFramework contamination was introduced.

## Canonical model
- [ ] Transitions are explicit and graph validation exists.
- [ ] Approval and escalation policies are explicit entities, not free-text conventions.
- [ ] Work briefs and baton handoffs are first-class runtime artifacts.
- [ ] Override paths are explicit, bounded, and journaled.
- [ ] Rights/permissions can be narrowed by process step governance.

## Operational control
- [ ] Blocked / approval-needed / intervention-required states project into project structure.
- [ ] Journal and replay surfaces expose the same canonical runtime events used by overlays.
- [ ] Customer/internal-customer feedback can be attached to completed runs where required.
- [ ] Conformance and deviation clustering do not create unmanaged rumor registries.

## Code quality
- [ ] No new god service or oversized component was introduced.
- [ ] New helper/services have clear ownership and do not duplicate canonical state.
- [ ] Child persistence preserves stable identity where needed.
- [ ] Runtime concurrency protection is in place.

## Testing
- [ ] Unit tests cover graph validation, approval conflicts, escalation routes, and transition rules.
- [ ] Integration tests cover handoffs, project-structure propagation, and bridge behavior.
- [ ] MCP tests cover new journal/read/query surfaces.
- [ ] Concurrency tests cover conflicting claims and step transitions.
