# C# pattern selection records

| Decision | Selected pattern | Why | Rejected alternative | Gate |
|---|---|---|---|---|
| Logical path representation | Immutable typed value plus explicit boundary conversion | Removes separator magic strings and makes persisted format testable. | More string helpers would preserve ambiguous semantics. | A01/C1a |
| Physical root behavior | Policy/service in Infrastructure | Contains I/O, host case behavior, links, and permissions at the infrastructure boundary. | Putting I/O in SharedKernel would reverse dependency intent. | A02/C1 |
| OS behavior | Narrow capability plus leaf adapter selected in composition | Makes unsupported behavior explicit and independently testable. | A broad `IPlatformService` becomes a service locator and hides authority. | A05/C3a |
| Storage/control-plane migration | Versioned reader + staged backup/verify/commit/rollback | Existing host-bound records need deterministic recovery. | Parse-and-silently-default would hide data loss. | A03/C2a |
| Secret provider | Strategy selected from an explicit profile | Separates interactive and headless secure stores without insecure fallback. | Raw file key beside ciphertext and unsupported auto-selection are not acceptable. | A04/C2 |
| Durable file replacement | Same-directory temporary file + flush + atomic replace/rename + recovery marker where multi-record | Preserves crash consistency across supported filesystems. | Direct overwrite can truncate state. | A02/A03 |
| Process execution | Typed immutable execution plan and one low-level executor | Makes argv, environment, timeout, redaction, and ownership explicit. | Shell-command strings and duplicated runners are platform-coupled. | B01/R1 |
| Existing partial clusters | No new split unless an independent responsibility/test seam is extracted | Prevents cosmetic architecture claims. | Moving methods between partial files does not reduce coupling. | Every phase |

All decisions remain provisional until their owning closure gate has behavioral proof. There is no approved fallback that converts a security, path, or capability error into success.
