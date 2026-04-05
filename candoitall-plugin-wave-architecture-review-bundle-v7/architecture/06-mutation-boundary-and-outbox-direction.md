# Mutation boundary and outbox direction

## Why this matters now

The next connector wave is likely to introduce externally visible side effects.

## Current weakness

Workbench currently mutates local state and then reconciles CRM/HR via compensation.

## Target direction

- use a single transaction where all participants share the same local boundary
- where that is impossible, use an outbox / saga style durable orchestration
- do not allow outbound connectors to perform irreversible side effects directly from local UI mutation handlers
- record recovery state explicitly

## Practical rule

The first connector platform phase may ship without outbound mutation features, but only if those features are explicitly disabled until the mutation boundary is ready.
