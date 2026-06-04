# Runtime boundaries

## Approval boundary

Inputs:
- Workflow id/version/run id.
- Node id and executor id.
- Redacted settings summary.
- Permission policy and capability flags.
- Optional preview/live mode.

Outputs:
- Approved/denied/timed-out decision.
- Approval response JSON.
- Audit/event records.

Rules:
- Secrets never appear in approval text.
- Approval decisions are per run and per node attempt.
- Live external writes require explicit approval unless a signed policy exception exists.

## Event boundary

Inputs:
- MAF workflow events.
- CanDoItAll node progress events.
- Executor audit events.
- Custom domain events.

Outputs:
- `WorkflowEventRecord` with kind, node id, executor id if available, request id if available, safe message, payload reference or redacted inline payload.

Rules:
- Do not use raw `ToString()` as the only persisted message.
- Do not store large payloads inline beyond artifact policy.
- Always preserve enough correlation for UI drill-down and postmortem analysis.

## Checkpoint boundary

Inputs:
- MAF checkpoint manager/storage.
- Superstep events.
- Pending request state.

Outputs:
- Checkpoint metadata records.
- Trusted checkpoint storage reference.

Rules:
- Checkpoint blobs are trusted private infrastructure.
- Do not expose raw checkpoint blobs to normal workflow users.
- Resume must validate workflow definition identity/version compatibility or explicitly require a migration path.

## Plugin executor boundary

Inputs:
- Plugin descriptor.
- Plugin workflow executor descriptor.
- Plugin grants.
- Connection/secret/OAuth capability metadata.

Outputs:
- Runtime executor descriptors.
- Availability descriptors.
- Audit records.
- Plugin logs.

Rules:
- Permission policy must be consistent with plugin capabilities.
- Host command and external write capabilities are never silently downgraded.
- Deterministic test mode must avoid live external effects.
