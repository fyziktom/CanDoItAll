# Target Solution

## Production Scope Allowed In This Bundle
- Refactor existing `.NET/Rust transcript verifier` internals.
- Harden the process read-only adapter.
- Add a second verification-only alpha for runtime evidence consistency if it stays payload-only and read-only.
- Add tests and docs for Office/business read-only lane denials.
- Update Core/driver roadmap and release gates.

## Production Scope Denied
- Runtime driver registry, selector, host, provider, DI, manager command, scheduler/workflow integration.
- Command execution, shell access, package restore, Graph/Office runtime calls.
- Workspace/storage/DB/process mutation.
- Claims, transitions, finalizer application, retry scheduling, provider repair.
- Broad Core runtime extraction.
