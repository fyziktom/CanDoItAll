You are Codex working in the private repository `fyziktom/CanDoItAll`.

Target branch:
- `db-remove-sqlite`

Before coding:
1. Read the repository-local bundle skills:
   - `codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
   - `codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
2. Read this bundle:
   - `codex/bundles/db-postgres-canonicality-throughput-followup-v3/README.md`
   - all subbundle READMEs under `subbundles/`
   - `analysis/03-db-bottleneck-inventory.md`
   - `requirements/02-canonicality-invariants.md`

Mission:
The previous work removed SQLite from the main runtime and introduced a canonical PostgreSQL runtime profile. Now complete the next hardening wave: remove remaining SQLite-era/hot-switch bottlenecks, preserve one canonical runtime DB source of truth, and safely unlock PostgreSQL concurrency.

Execute subbundles in order:

1. SB01 — validation evidence and scope cleanup
2. SB02 — canonical runtime vs pending activation contract
3. SB03 — remove dead hot-switching and drain state
4. SB04 — maintenance profile context factory boundaries
5. SB05 — PostgreSQL claimed-work parallel processing
6. SB06 — process dispatch claim-token canonicality
7. SB07 — claim-first dispatch candidate loading
8. SB08 — final validation, benchmark, and merge gate

Hard constraints:
- Do not reintroduce SQLite in the main runtime.
- Do not modify `CanDoItAll.IPFS`.
- Keep comments in code in English.
- Do not weaken process canonicality to gain speed.
- Do not mark a subbundle complete without proof artifacts under `proof/SBxx`.
- Do not hide failed validations. Record failures and either fix them or mark explicit residual risk.

Key issues to address:
- `DatabaseRuntimeSwitching.cs` still contains drain/lease/hot-switch mechanics even though normal `AppDbContext` creation no longer uses it.
- The UI/API/domain needs to expose two different concepts: the canonical runtime profile currently used by this process, and the persisted active profile that will be used after restart.
- `DatabaseOptions.EnableMaintenanceHotSwitch` exists but is effectively only logged; remove it or make it a true explicit maintenance-only feature with strong proof.
- Automation, connector, and process outbox paths use PostgreSQL batch claim, but still process claimed records sequentially.
- Process dispatch claim renew failures are warnings; stale executions should not be able to commit transitions after claim loss.
- `LoadDispatchCandidateAsync` performs heavy candidate loading before a durable claim; move toward claim-first or at least reduce full-run scans/N+1 calls.

Final proof required:
- `dotnet restore .\CanDoItAll.slnx`
- `dotnet build .\CanDoItAll.slnx -m:1 -v:minimal`
- unit tests
- focused integration tests for DB profile activation, process dispatch leases, automation/connector/process outbox claim concurrency
- Data Sources browser proof for runtime vs pending restart profile
- broad integration suite, or explicit environment-limited failure with exact reason
- residue audit for retired SQLite provider in runtime source/test scope
- source assertion audit for no normal hot-path `AcquireContextLeaseAsync` or `BeginSwitchAsync`
- PostgreSQL concurrency stress proof showing no duplicate processing and no stale claim commits
