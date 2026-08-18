# PostgreSQL and real-host API proof

Command:

```text
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj
  --no-build --no-restore -nologo -v:minimal
  --filter FullyQualifiedName~Profile_switch_before_finalization_retains_committed_usage_and_blocks_later_writes
```

The exact database command ran outside the filesystem sandbox because the test control plane requires
its LocalAppData lock. Result: exit 0; 1 passed, 0 failed, 0 skipped in 8 seconds.

The deterministic barrier is after real provider invocation/audit but before assistant finalization.
After switch publication the HTTP operation returns 409 with `RuntimeProfileChanged`. Direct PostgreSQL
inspection proves:

- operation remains Running with `ProviderDispatchReturnedAtUtc` populated;
- successful invocation audit remains committed;
- usage retains 10 input, 4 output, and 1 cached-input token;
- the active turn remains present for recovery;
- no assistant transcript message was committed;
- a later request through the stale host also returns `RuntimeProfileChanged`.

Two compile-only corrections were made before the executable run: the test wrapper needed the Ports
namespace and the usage assertion needed `CachedInputTokens`. A sandboxed execution then failed only on
the control-plane lock; the unchanged outside-sandbox rerun passed.
