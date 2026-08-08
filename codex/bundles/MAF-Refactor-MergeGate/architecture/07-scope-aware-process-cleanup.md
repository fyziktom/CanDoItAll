# Scope-aware durable process cleanup

## Failure topology

```text
organization workspace service persists execution run
  -> floating Project Structure turn admits Project X authority
  -> MAF creates Project X per-run workspace services
  -> project-scoped command service writes durable process lease
  -> execution completes
  -> organization workspace's fixed-scope cleaner enumerates organization lease root
  -> Project X lease is not found
```

## Target

Terminal cleanup derives the effective workspace scope from trusted persisted run metadata/governance,
validates it against the run, and creates a scope-matched cleanup executor. The execution store may
remain organization-scoped; lease enumeration and `startup.json` stop operations must use the original
effective run scope.

Cleanup remains:

- terminal-state gated;
- idempotent;
- durable-claim protected;
- non-authoritative over the primary run outcome;
- retryable when scope or receipt evidence is invalid.
