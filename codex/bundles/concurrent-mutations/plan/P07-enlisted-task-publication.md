# Enlisted task publication authority

Status: implemented after signed selected-source checkpoint `c2ef21ae8`.
The [validation record](../validation/2026-10-20-enlisted-task-publication.md) has
72 correctness passes and one failed performance reference. Performance closure is open.

The task publisher currently calls its held authority both before opening a transaction
and inside it. A Process source fence requires the already-enlisted effect transaction;
putting it in the current-check method would call it too early. Add a required explicit
`RequireEnlistedAsync` entry to the existing held-authority contract, with no default or
anonymous callback. Replace the two checks immediately before staging and after flush
with this entry. Keep the pretransaction current check and original receipt paths.

The first enlisted check runs after the exact Project gates, lifetime checks and current
basis read, but before any task/receipt staging. The second runs after flush and before
the final Project/epoch/policy checks and commit. Implementations must include their
current check. The existing interactive Agent authority delegates to that same check;
it does not acquire another catalog lease or add SQL. The inherited adapter will also
invoke the exact selected Process fence. Workbench's publisher stays source-neutral and
does not depend on Process persistence, Agent runtime or particular source protocols.

Current Project gates precede the Process root. Every source lock remains owned by the
effect transaction even when the enlisted context is disposed. Exceptions before commit
roll back tasks and receipt together. A completed original receipt remains recoverable
after later source expiry without new mutation admission. No new transaction, lock,
retry loop, cache, project reference or schema is introduced.

Use the actual PostgreSQL task publisher with an explicit test authority wrapping the
real selected Process fence and a trusted prepared Project source. Prove early refusal
before staging, expiry after business flush rolling back tasks and receipt, successful
publication, and original receipt recovery after expiry without acquiring new source
authority. These are boundary tests; their opaque Process fixture material does not claim
the later task-plan material adapter is complete. Repeat relevant existing owner, currentness,
active-work, ordinary Agent and selected Process publication tests. Confirm exact discovery,
build affected production/test projects, full portability enforcement, architecture delta,
frozen observed execution and resource reconciliation before a signed checkpoint.

Inherited target/basis/participant, parent-owned Core/MAF saved continuation, actual Process
and Workflow producers, manager authorization and atomic accounting remain separate required
integration work. This hook alone does not enable them or close R5/R6.
