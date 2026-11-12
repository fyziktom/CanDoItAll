# Processes UI tests

Light component tests for the shared Processes renderers, opening-owned drafts,
submission reconciliation, nested files and manager conversation presentation.
The dependency guard rejects forbidden transitive references, missing relevant assemblies
and native services in public component parameters.

The current source-expanded selection contains 21 cases. Build the owning project and
discover `FullyQualifiedName~CanDoItAll.Tests.Components.ProcessesUI` before executing the
same filter and configuration. These checks use deterministic scenario data and do not
establish native persistence, authorization or browser behavior.

Native owner, integration and browser selections are documented in
[Testing](../../../docs/testing.md#processes-ui-slice). The
[architecture record](../../../docs/architecture/processes-ui-boundary.md) describes the
production boundary and evidence limits.
