# C# boundary map

## Approved ownership

| Concern | Contract owner | Implementation owner | Forbidden coupling |
|---|---|---|---|
| Logical workspace path | `CanDoItAll.SharedKernel` | Consumers normalize at their input boundary | No filesystem calls or host probing in the value contract. |
| Opaque external-target alias | `CanDoItAll.SharedKernel` pure codec | Physical owners bind and resolve through the narrow port | Alias text must not disclose a physical root; the codec performs no I/O or host probing. |
| Physical path/root | `CanDoItAll.Infrastructure.Abstractions` narrow binding/resolution port | `CanDoItAll.Infrastructure` filesystem/storage policies | MAF models/core may reference only the port and opaque protected binding record, never the Infrastructure implementation or raw physical root. |
| Host capabilities | Narrow contracts in their consuming abstraction project | Composition selects leaf adapters | No broad `IPlatformService` and no capability that grants authorization. |
| Secret reference | `CanDoItAll.Security.Abstractions` | `CanDoItAll.Modules.Security` | No insecure automatic fallback or raw secret in receipts/logs. |
| Generic process execution | MAF runtime/core boundary | Runtime leaf adapters | No Workbench UI or Processes domain semantics in the primitive. |
| Runtime-node meaning | Workbench | Workbench compiler/orchestrator | Workbench does not own low-level process lifecycle. |
| Supervision/recovery | Manager | Manager OS-specific leaf adapters | No name-only termination. |
| Process-domain semantics | Processes | Processes drivers/runtime | MAF capability facts cannot override domain policy. |
| File browsing/opening | FileTools contracts plus product integration adapter | FileTools and `CanDoItAll.FileTools.Integration` | Shipping FileTools must not depend on CanDoItAll product source. |

## Logical-path owner decision

The smallest correct owner is `CanDoItAll.SharedKernel`. Both Infrastructure and MAF Core already depend on it, it has no reverse dependency, and the contract is a pure value with no I/O. A new project would add graph and packaging cost without creating a distinct deployable boundary. The permitted change is deliberately narrow: typed logical-path parsing, canonical `/` serialization, segment access, equality, and explicit conversion at physical boundaries.

SharedKernel must not acquire environment discovery, filesystem access, process execution, storage, or provider selection. If A01 cannot preserve that constraint, the decision must return to the architecture gate before code changes.

## External-target binding decision

A01 requires restart-safe external-target aliases that neither expose a physical root nor
grant a process-global physical-path authority. A stateless reversible alias cannot meet
that non-disclosure requirement. The approved shape is therefore:

- `CanDoItAll.SharedKernel` owns only the pure versioned alias syntax, segment codec, and
  structural comparer;
- `CanDoItAll.Infrastructure.Abstractions` owns the minimal physical-boundary port and an
  opaque host-bound protected binding record;
- `CanDoItAll.Infrastructure` owns Data Protection, host checks, physical containment,
  binding creation, and resolution;
- composition supplies a registry per request/runtime scope, while invocation factories
  reconstruct a registry from only that invocation's persisted bindings.

The abstractions assembly has no dependency on SharedKernel, Infrastructure, MAF, or any
host API. It exists because placing the port in SharedKernel would make the logical-value
owner a physical authority, while making MAF reference Infrastructure would reverse the
approved dependency direction.

## External repositories

`CanDoItAll.Components` and `CanDoItAll.FileTools` remain independent repositories. Temporary direct project references are an execution-time development topology, not permission to reverse their dependency direction. Any required source change triggers a child-bundle record in B00 and must be built/tested in the owning repository before consumer validation.
