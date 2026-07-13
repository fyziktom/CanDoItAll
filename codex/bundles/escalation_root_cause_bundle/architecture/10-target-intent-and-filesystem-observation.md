# Target intent is template-owned; filesystem observation is generic (2026-07-12)

## Trigger

A fresh greenfield delivery run produced a `.NET solution context` with
`verify-existing` and an `external-target/...` alias embedded in a
product-root-relative path. The isolated .NET setup driver correctly refused
to verify a solution that did not exist, but the process had no authoritative
target-state fact to guide the architecture agent.

## Decision

The generic Workspace driver publishes a read-only
`ProductTargetFilesystemState` launch fact (`missing`, `empty`, `populated`,
`not-directory`, or `unavailable`). It obtains that fact through
`IWorkspaceFileService.StatPath` and does not interpret it as provisioning
intent.

The `slice-scope-packet` owns the semantic `ProductTargetState` decision:
`greenfield` means no authoritative baseline must be preserved; `existing`
means an authoritative baseline must be retained and modified. The architecture
template maps that decision to the existing typed .NET context:

- `greenfield` -> `initialize`
- `existing` -> `verify-existing`

The .NET driver remains deterministic. It validates that context paths are
product-root-relative and rejects `external-target/...` aliases, but it never
changes provisioning mode or derives topology from a directory probe.

## Responsibility split

| Responsibility | Owner |
| --- | --- |
| Read filesystem existence, kind, and direct child count | Generic Workspace launch driver |
| Decide whether an authoritative product baseline exists | Intake template and scope artifact |
| Translate semantic target intent to .NET provisioning mode | .NET architecture template and architect instructions |
| Validate .NET solution-context path syntax | Isolated .NET driver |
| Execute declared initialization or verification | Isolated .NET setup driver |
| Dispatch, retry, and process state transitions | Generic Processes runtime/dispatcher |

## Pattern selection record

The observed problem force is a launch-time fact with a reusable, bounded
filesystem query. A launch-variable contributor is selected because it is an
existing extension seam, has no product or provider SDK dependency, and can be
unit tested with `IWorkspaceFileService`. A new generic runtime branch was
rejected because it would make every enterprise process understand a .NET
provisioning decision. A .NET-driver inference was rejected because an empty
directory does not establish product intent.

## Acceptance criteria

- The generic runtime and dispatcher contain no .NET or app-specific target
  interpretation.
- Subprocesses recompute the physical target observation rather than inheriting
  a stale parent value.
- Templates distinguish semantic intent from physical state and record their
  evidence before emitting a solution context.
- The .NET resolver rejects both slash forms of `external-target` aliases in
  root-relative fields.
- Unit and composition tests prove the contributor, path rejection, and
  subprocess recomputation independently of a live agent provider.

## Completion validation and launch-fault boundary (2026-07-12)

The next fresh run proved that a schema-labelled architecture artifact can be
well-grounded as a managed file while still being semantically invalid. The
architecture agent emitted `provisioningMode: initialize` without the required
initialization object. The isolated .NET launch contributor rejected it only
when the setup subprocess was starting. That exception escaped the subprocess
coordinator, so the generic dispatcher released and reclaimed the same step
without a durable diagnostic.

`PayloadSchema` is therefore carried as generic artifact metadata from the
template through the runtime step contract. The generic completion-gate pipeline
receives that contract but does not interpret any schema. An isolated .NET
completion-gate contribution activates only for a descriptor that explicitly
declares `dotnet.solution-context/v1`, reads its managed artifact, and delegates
to the existing typed parser. It returns an ordinary completion issue when the
schema is invalid; the recovery classifier permits one idempotent correction
attempt for the same diagnostic fingerprint and then requires manager review.

The subprocess coordinator is also the universal exception boundary for the
launch coordinator. A non-cancellation launch fault first checks whether a
child was created; if so, the parent remains deferred for that child. If not,
the coordinator emits a non-retryable generic subprocess-launch issue. The
dispatcher never sees a domain exception and therefore cannot turn it into a
claim-release retry loop.

| Responsibility | Owner |
| --- | --- |
| Declare an artifact's payload schema | Process template |
| Transport schema metadata | Generic template/application/runtime contracts |
| Interpret and validate `dotnet.solution-context/v1` | Isolated .NET completion driver |
| Convert launch-coordinator faults into durable outcomes | Generic subprocess coordinator |
| Decide retry versus manager review | Generic recovery classifier |

The selected patterns are the existing ordered completion-gate chain for
schema-specific validation and the existing subprocess coordinator as an
exception-to-outcome adapter. A new global validator registry was rejected for
now because it would add a one-implementation abstraction; the current
schema-keyed contribution is independently testable and keeps the generic
pipeline free of .NET behavior.
