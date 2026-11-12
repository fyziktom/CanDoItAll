# P01a cleanup validation

Candidate: first production change after architecture commit `ceb10731e`.
Proof tier: Behavioral. Owner: Infrastructure resource lifecycle only.

## Implemented behavior

Transaction disposal runs inside `try/finally`, so every acquired process semaphore is
released even when disposal throws. Failed acquisition preserves its original exception;
if cleanup also fails, an `AggregateException` preserves acquisition first and cleanup
second. Disposal remains idempotent. Isolation, lock keys, stripe count and public API
are unchanged. No new production type, reference, partial class or abstraction was added.

The test-only EF transaction factory disposes the actual PostgreSQL transaction before
throwing. Same-key InMemory contenders prove that process semaphores were released,
independently of PostgreSQL lock release. A successor holding the keys cannot be
unlocked by repeated disposal of its predecessor. The cancellation case proves that
the acquired prefix is released while the later lock remains held elsewhere.

## Executed checks

All commands ran with ordinary `Release` outputs and serialized builds. The four
PostgreSQL cases used the single owned PostgreSQL 18.6 fixture and existing database
leases; credentials were supplied through the test environment and not logged.

```powershell
dotnet build src/Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj --configuration Release /m:1 --nologo
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --list-tests --filter 'FullyQualifiedName~SerializableMutationScopeTests' /m:1 --nologo
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~SerializableMutationScopeTests' /m:1 --nologo --logger 'trx;LogFileName=p01a-components.trx' --results-directory .artifacts/concurrent-mutations/validation
python tools/Validation/Portability/scan_portability.py --repo-root . --output .artifacts/concurrent-mutations/validation/p01a-portability.json --tracked-only
python tools/Validation/Portability/enforce_portability_baseline.py --scan .artifacts/concurrent-mutations/validation/p01a-portability.json --baseline tools/Validation/Portability/portability-risk-baseline.json
```

- Infrastructure build passed. Existing NU1902/NU1903 dependency warnings remain.
- Components assembly refreshed successfully; existing unrelated compiler/analyzer and
  package warnings remain. Expected discovery six; actual discovery six.
- Focused result: six passed, zero failed, zero skipped (two seconds test duration).
- Full scan: 8,187 files, 36,107 findings; enforcement passed with all 15,419 reviewed
  executable-source findings unchanged. No baseline refresh was needed.
- Documentation validation passed for 424 maintained Markdown files;
  `git diff --cached --check` passed.
- Unchanged portability tooling self-tests passed earlier in this execution: six
  enforcement tests, four secret-scanner tests and seven rendering-tool tests.
  Rendering register passed for 1,188 inputs. This repair changes no rendering input.

Ephemeral logs/TRX/full scans remain in `.artifacts/concurrent-mutations/validation`.
The durable evidence is this scoped behavioral record, not a claim that the whole
solution or concurrent-mutation initiative is complete.

## Closure

Independent UI/validation reviewer found no blocking defect: primary error ordering,
unconditional release, acquired-prefix cancellation and successor-lock idempotence are
supported by the source and six cases. Parent review agrees. Architecture closure is
Pass for this resource-lifecycle repair; no project/dependency/partial-class changes.
The phase acceptance and negative/dependent-use proof are complete. The broad
stable checkpoint remains scheduled at the frozen integrated foundation because this
helper is shared persistence infrastructure. It is not repeatedly run for each small
prerequisite commit. The fixture remains in use for the next database/performance lane.
