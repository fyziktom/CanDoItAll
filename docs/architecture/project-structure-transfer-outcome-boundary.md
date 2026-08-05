# Project Structure transfer outcome boundary

## Decision

Project Structure copy, move, and subproject-transfer behavior owns application results,
rejections, compensation evidence, and partial-commit recovery in the Workbench module.
Those contracts do not derive from or expose agent transport exceptions.

`ProjectStructureAgentService` is the adapter boundary. It maps application failures to
the existing agent status code, error code, safe-message policy, retry policy, and details
shape. Blazor handles the application failures directly and never catches
`ProjectStructureAgentException` for shared transfer behavior.

## Responsibility and dependency direction

| Owner | Responsibility |
|---|---|
| Workbench transfer models | Typed rejection reasons, compensation, recovery, and canonical transfer evidence. |
| Subproject transfer coordinator | Create, transfer, validate the returned target, and compensate an empty child. |
| Cross-module mutation service | Commit Workbench state and emit application recovery evidence when reconciliation remains incomplete. |
| Project Structure UI | Translate application outcomes into user feedback. |
| Agent service | Translate application outcomes into the stable agent failure contract. |

The dependency direction is UI/agent adapters -> Workbench application behavior. Shared
Workbench behavior does not depend on agent HTTP/tool semantics or on public persistence
state enums.

## Result invariants

- `ProjectStructureSubprojectTransferResult.TargetProjectId` is the authoritative target.
- A coordinator success is rejected when the transfer result identifies a target other
  than the reserved child.
- `ProjectStructureCreatedSubprojectTransferResult.TargetProjectId` is derived from its
  transfer result.
- Agent `MovedNodeCount` is derived from `MovedNodeIds`.
- Project creation rejection is converted from `Result` in one shared guard.

## Pattern selection

The selected pattern is an adapter: application failures are normalized once at the
agent boundary. A new interface, strategy, project, or exception hierarchy was rejected
because there is one closed mapping and no independent implementation or deployment
lifecycle. Keeping transport fields on application exceptions was rejected because it
would preserve the original dependency inversion.

## Testability contract

- Coordinator unit tests reject mismatched target evidence and prove compensation.
- Mapper unit tests prove application rejection, compensation, creation rejection, and
  partial-commit recovery retain the external agent contract.
- Existing component and integration tests prove UI and persistence composition.
- Focused tests require no live provider, browser, network, or running web host.
