# Historical receipt authority: two Unit source cases

Status: authored in the managed continuation checkout; not built, discovered or executed. No main-checkout source, production implementation, database or process was changed by this subset.

`CanonicalAgentExecutionAuthorityResolverTests.HistoricalRead.cs` adds exactly two `[Fact]` methods in `CanDoItAll.Tests.Unit.AgentFramework.CanonicalAgentExecutionAuthorityResolverTests`. It has no theories, providers, category exclusions or skips. Source accounting of all three partial files is 19 Facts plus 20 InlineData rows = 39 prospective rows (37 existing plus two new). Roslyn parsing reported zero syntax errors; that does not establish type correctness or runtime success.

The first Fact calls the actual canonical resolver with saved admission generation 8 and current host generation 0. It asserts that the separate historical read returns only a read policy, preserves the original governance/source objects and lifetime, uses two read-only catalog observations and one source observation, and never acquires a catalog lease or ordinary workspace service. Ordinary execution resolution of the same original admission still refuses the generation mismatch. Named arrangements cover current source read, scope and lifetime revocation, source-owner denial, missing/inactive current Agent, removal after source observation, changed catalog revision, and host-generation changes during final catalog observation or a positively observed pending source read. The pending operation is released and awaited in `finally`.

The second Fact proves unsupported read capability cannot fall back: a lease-only catalog, default resolver observer, missing source provider and legacy admission without its original lifetime remain unsupported. A changed saved runtime fingerprint denies before catalog/source reads. The four unavailable catalog statuses throw read-unavailable `IOException` before ordinary workspace, lease or source access. Diagnostic labels identify the finite arrangement without printing private source payloads or connection fingerprints.

The controlled catalog/source objects are test doubles around the real canonical resolver. They do not prove filesystem read behavior, actual owner receipt disclosure, cancellation of real I/O, two-process restart, HTTP wire shape or browser behavior. Those remain separate integration gates. These tests neither publish a journal checkpoint nor admit a continuation and cannot clear file-lease or activity-finalization uncertainty.

Prospective narrow filter (expected 2):

```text
FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.CanonicalAgentExecutionAuthorityResolverTests.Historical_read_preserves_original_admission_and_requires_current_source_catalog_and_host_fences|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.CanonicalAgentExecutionAuthorityResolverTests.Historical_read_requires_explicit_capability_and_never_falls_back_to_ordinary_authority
```

Parent validation must refresh affected standard Release outputs, confirm the two-row discovery before execution, and include the final repository portability gate. This source-only record makes no passing-test claim.
