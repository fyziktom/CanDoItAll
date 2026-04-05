## Capability registry and party-assignment ownership

The node-kind registry should grow into a capability owner, not stay only as a label/visual/profile registry.

Recommended additions:

- allowed party-assignment roles
- whether canonical node scope is required
- participant-role resolution policy
- allowed reference kinds
- allowed command surfaces
- allowed plugin/workbench hooks
- migration/reclassification policy

Then:
- workbench page code asks the registry which roles are valid,
- CRM/HR validation asks the registry whether a role is legal for that node,
- future plugins/agents can inspect the same capability matrix.

This prevents semantic drift between UI, services, and agents.
