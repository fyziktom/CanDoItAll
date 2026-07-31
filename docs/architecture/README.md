# Architecture

The architecture documentation is intentionally small:

- [Overview](overview.md) defines boundaries and dependency direction.
- [Internal communication](internal-communication.md) explains in-process, persistence,
  HTTP, event-stream, provider, and plugin communication.
- [Modules](modules.md) maps product modules to their responsibilities and entry points.
- [Process outcome authority with MAF 1.15](process-maf-1.15-outcome-authority.md)
  records the finalizer, managed-artifact, and branch-aware preflight boundary.

Detailed operational contracts live beside their subject in the parent documentation
directory. Project READMEs describe the local project boundary and validation command.
