# SB07 Runtime and Validation Closure

## Real mini-model validation

- Host: managed rebuilt CanDoItAll Web instance.
- Provider: `OpenAI default`
  (`c1c103db-707e-3f52-8809-8d804fc171d1`), healthy.
- Agent: `Product owner AI agent`
  (`10ad7a84-a717-4a9e-aba3-06a275de310c`), zero capabilities.
- Model: `gpt-5.4-mini`.
- Invocation policy: one tool-free exact-response request, no retry.
- Result: HTTP 200 in 23.5 seconds; response `MINI_SMOKE_OK`.
- Durable execution run:
  `36cc271d-dab0-46d4-915e-ca1c2ebaab4e`.
- Durable initial activity operation:
  `5364ac0c-bbf0-4cf8-900e-2b8c075122fc`.
- Persisted agent/provider configuration was not changed.
- Terra was not used.

The durable operation ID is correlation metadata. It is not an SSE URL,
subscription handle, or promise that in-process activity history is durable.

## Automated validation

| Validation | Result |
| --- | --- |
| Focused architecture unit suite | 140/140 passed |
| Selected activity/chat/process component suite | 95/95 passed |
| File persistence, scaling, usage, and WAL integration groups | 59/59 passed |
| Project Structure HTTP read-source boundary | 1/1 passed |
| Non-chat execution progress tracking regression | 1/1 passed |
| Managed canonical provider seed integration | 3/3 passed |
| Serial solution build | 0 errors, 166 warnings, 48.64 seconds |
| Final managed serial rebuild | Exit code 0, 47.701 seconds |

The final build command was:

```powershell
dotnet build CanDoItAll.slnx --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -nologo -v:minimal
```

Warnings include the already-disclosed `System.Security.Cryptography.Xml` 10.0.7
NU1903 advisory and one pre-existing xUnit2029 warning. They are not represented as
new errors or silently suppressed.

An additional no-build solution-wide test attempt emitted 15 failures across
unchanged unrelated component-test areas and then stalled for more than ten minutes.
The exact test parent, VSTest, and test-host processes were stopped; the managed Web
host was not touched. The failures predominantly report an
bUnit/AngleSharp `MissingMethodException` or missing legacy bUnit JS/service
registrations. This broad repository harness is explicitly not claimed green and is
separate from the 95/95 change-focused component gate.

## SharedInfo validation

- Both live OpenAPI routes produced byte-identical HTTP-only documents.
- Generated document: 438,706 bytes, 234 paths, 279 operations, 347 schemas.
- Server: `http://localhost:5032/`.
- SHA-256:
  `BD1F0B297956E4CEB176AA183FE283BB481D20CD686CAF075B52881BD7E92AEC`.
- `Test-CanDoItAllWebOpenApi.ps1`: pass, `FailureCount 0`.
- `Test-SharedInfo.ps1`: pass, 43 skills, 396 Markdown files,
  12 PowerShell files, `FailureCount 0`.

## Architecture and UI validation

- CodeAnalytics snapshot: `snap-20260728014834-63e19a8b`.
- Affected inventory: 12 projects and 963 documents.
- Affected project graph: acyclic.
- Blocking architecture findings: none.
- Disclosed pre-existing debt: three intra-project module cycles and two nested-type
  cycles outside the affected path.
- Browser proof: seven reviewed `1920x1080` floating/manager states, zero console
  errors, zero console warnings, no horizontal overflow, hidden Blazor error UI,
  and no stale terminal spinner.

## Final live host

- Logical app: `candoitall-web-5032`.
- Managed session: `app_c4340c64f0b3453e8f3f45bb687f8bc5`.
- URL: `http://localhost:5032`.
- Watch generation: `candoitall-web-5032:1:g0`.
- State at closure: healthy and waiting for changes.
- Watcher PID at start: 22740.
- Runtime PID at start: 41736.
- Startup override:
  `--Processes:RuntimeDispatchQueue:EnableRecovery=false`.

The prior rebuilt session became unhealthy when the known legacy recovery scan
repeatedly faulted `ProcessRuntimeDispatchQueueWorker`. The final session disables
only that recovery scan through command-line configuration; immediate dispatch and
the Process Manager/agent paths remain enabled. An attempted environment overlay was
rejected by the managed-host allowlist and was not used.

The host remains running for user testing. Final PID ownership and HTTP health are
rechecked immediately before handoff because watch-mode runtime PIDs may legitimately
change after a rebuild.
