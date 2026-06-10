# Audit Persistence Decision

## Required outcome
Production process runtime must use EF-backed audit persistence by default. In-memory audit store is allowed only for explicit isolated tests and standalone helper constructors.

## Required proof
- DI resolves `IProcessVerificationAuditStore` to `EfCoreProcessVerificationAuditStore` in the full app/process module composition.
- `IProcessVerificationAuditQueryService` reads the same stored records after scope restart.
- requester identity is redacted before persistence.
- audit query limits and filters work by process run, step run, and lane.
- migration/bootstrap proof exists.
