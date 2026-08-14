# Architecture

The architecture documentation is intentionally small:

- [Overview](overview.md) defines boundaries and dependency direction.
- [Storage, paths, and host portability](storage-and-path-portability.md) defines provider
  dispatch, logical locators, host-bound filesystem roots, and cross-host rebind.
- [Runtime execution and shell portability](runtime-execution-portability.md) distinguishes
  typed runtime plans, POSIX `sh`, file-skill Bash, terminals, and host capabilities.
- [Internal communication](internal-communication.md) explains in-process, persistence,
  HTTP, event-stream, provider, and plugin communication.
- [Modules](modules.md) maps product modules to their responsibilities and entry points.
- [Process outcome authority with MAF 1.15](process-maf-1.15-outcome-authority.md)
  records the finalizer, managed-artifact, and branch-aware preflight boundary.
- [Provider model-parameter negotiation](provider-model-parameter-negotiation.md)
  defines request-feature-aware compatibility before provider dispatch.
- [Project Structure transfer outcome boundary](project-structure-transfer-outcome-boundary.md)
  separates shared transfer recovery from agent transport failures.
- [Agent tool failure recovery boundary](agent-tool-failure-recovery-boundary.md)
  defines safe, retryable tool failures without exposing arbitrary exceptions.

Detailed operational contracts live beside their subject in the parent documentation
directory. Project READMEs describe the local project boundary and validation command.
