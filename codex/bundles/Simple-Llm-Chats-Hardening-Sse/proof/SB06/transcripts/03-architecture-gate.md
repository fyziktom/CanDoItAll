# CP1 architecture review gate

Status: Pass.

- Canonical/product owners remain in domain/application contracts and services.
- EF adapters own persistence, transactions, bounded SQL, and the database lease heartbeat.
- The `IDbContextFactory<AppDbContext>` heartbeat adapter opens short lifecycle-observation contexts; it
  does not execute product command transitions or replace the scoped fenced unit of work.
- Composition owns hosted lifetime and DI wiring. Web owns transport mapping only.
- The public inline engine execution method is gone; durable dispatcher ownership is structurally
  enforced at the call graph.
- CodeAnalytics snapshot `snap-20260815041852-376a68b7` covers six projects with 570 types, 3,794
  members, 38 service registrations, zero cycles, zero diagnostics, zero open questions, and no
  error-level findings.
- Static guards report no forbidden reference, offset path, second execution caller, or production
  partial expansion.

Decision: CP1 Ready.
