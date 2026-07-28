# SB06 Browser Proof

## Scope

The screenshots and accessibility snapshots exercise the production floating-agent
chat and Process Workspace Manager chat at `1920x1080`. Both surfaces use the
production `IAgentChatExecutionOrchestrator`, current-profile activity reader, and
workspace persistence path. The only deterministic seam is the local
`scenario://harness` provider; no component service or activity reader was replaced.

## Floating chat

| State | Evidence | Finding |
| --- | --- | --- |
| Busy | `final-floating-busy.png`, `final-floating-busy.yml` | The current backend-controlled phase is visible above the composer while the operation is running. |
| Failure | `final-floating-failed.png`, `final-floating-failure.yml`, `final-floating-failed-geometry.json` | A real scenario failure is terminal, correlated to an operation id, and does not leave a stale spinner. |
| Approval | `final-floating-approval.png`, `final-floating-approval.yml`, `final-floating-approval-geometry.json` | Approval-required state is visible and the persisted approval actions remain actionable. |
| Completion | `final-floating-completed.png`, `final-floating-completed.yml`, `final-floating-completed-geometry.json` | Approval continuation obtains a new operation id, completes, and persists its assistant response. |

## Process Manager chat

| State | Evidence | Finding |
| --- | --- | --- |
| Busy | `final-manager-busy.png` | The first captured state is `Loading agent / Checking prepared agent and provider configuration`; it appears before run completion. |
| Approval | `final-manager-approval.png`, `manager-activity-observations.json` | The original operation remains correlated while suspended and all three approval actions are visible in the first viewport. |
| Completion | `final-manager-completed.png`, `final-manager-completed.yml`, `manager-activity-observations.json` | Continuation changes operation id, reaches `Completed`, and the SC04 completion response is present in the transcript. |

## Layout and accessibility review

- The status element renders with semantic `role=status`.
- The activity region and composer remain inside the existing chat surface.
- The transcript remains the only scroll owner; no nested activity scroll region was introduced.
- `document.scrollWidth` equals `clientWidth` in the measured Manager states.
- The activity and composer bounds stay within the `1920x1080` first viewport.
- `#blazor-error-ui` is hidden.
- Browser console result: two informational Blazor connection messages, zero errors,
  and zero warnings.

## Scenario-harness isolation

The first SC03 run exposed a separate generated-project build defect: the generated
app inherited the repository-wide `Directory.Build.targets` and attempted to copy the
full template catalog into its output. The activity UI correctly reported that
failure. A local generated-project MSBuild boundary and focused regression are part
of the closure validation; the failure screenshot remains useful negative-state
evidence rather than being reclassified as a successful scenario.
