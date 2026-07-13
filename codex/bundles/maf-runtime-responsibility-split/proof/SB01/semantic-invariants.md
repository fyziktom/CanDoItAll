# SB01 Semantic Invariants

- Invariant ID: `SB01-I01`
- Source raw note: `N001`, `N002`, `N006`.
- Expected behavior: Runtime responsibility inventory defines extraction boundaries before helper movement.
- Disallowed shallow implementation: Adding more partial classes while leaving helper, model, session, context, and finalizer logic owned by runtime.
- Failing-first test: N/A - inventory boundary; process/no production behavior was added.
- Passing test: `git diff --check` and later focused `dotnet test` transcript in `bundle://proof/SB01/transcripts/validation.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`.
- Production assertions: Runtime line count and symbol scan show extraction targets moved to focused collaborators.
- Red-team negative case: A catch-all helper class or new partial-only split would not satisfy the line-count and symbol scan proof.
- Downstream dependency check: SB02-SB07 manifests cite the boundaries established here.
