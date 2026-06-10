# Runtime Host Next Phase

The runtime host should advance from isolated dry-run pipeline to process-manager runtime diagnostics.

## Required evolution
- Promote stable request/result/readback DTOs to contracts where generic and useful.
- Keep implementation in Process Module where it touches process runtime services, EF, manager readback, scheduler/workflow job state, or UI/API.
- Add persistent lifecycle for read-only verification jobs.
- Add dry-run readback tied to actual process run/step context.
- Add static capability descriptors that can be displayed or queried without loading driver packages dynamically.

## Not allowed
- No execution-capable driver invocation.
- No shell/package/Graph/CRM/network/workspace/storage side effects.
- No reflection discovery or fallback selector.
- No driver self-registration.
- No Process Core dependency on drivers or runtime host.
