# A00 baseline report

## Windows actual host

Host: Windows `10.0.26200`, `win-x64`, .NET SDK `10.0.302`.

| Command | Exit/result | Evidence | Classification |
|---|---|---|---|
| `dotnet restore .\CanDoItAll.slnx --configfile .\NuGet.config` | 0 | terminal record; `windows-baseline/restore.log` also preserves the initial sandbox/config failure | Restore succeeds with the repository NuGet configuration. |
| `dotnet build .\CanDoItAll.slnx -c Release --no-restore /m:1` | 0; 0 warnings; 0 errors | `artifacts/unix-portability/A00/windows-baseline/build.log` | Clean baseline build. |
| Components stable suite | 954 passed; 0 failed | `artifacts/unix-portability/A00/windows-baseline/test-results/a00-windows-stable.trx` | Stable project proof. An earlier single failure passed on targeted rerun and is order-sensitive. |
| Unit stable suite | 5,296 passed; 1 failed | `artifacts/unix-portability/A00/windows-baseline/test-results/a00-windows-unit.trx` | Pre-existing time-sensitive assertion: expected and actual generated UTC text differed by one second. |
| Targeted unit rerun | 1 passed; 0 failed | `artifacts/unix-portability/A00/windows-baseline/test-results/a00-windows-unit-targeted-rerun.trx` | Confirms the baseline failure is timing-sensitive rather than a portability edit regression. |
| Integration project with `--blame-hang-timeout 2m` | External 10-minute timeout before discovery/output | `artifacts/unix-portability/A00/windows-baseline/integration-hang` | Pre-existing test-host/fixture startup stall; no portability product edit had occurred. |
| Artifact secret scan | 0 findings | `artifacts/unix-portability/A00/windows-baseline/secret-scan.json` | Pass. |

The original PowerShell baseline runner stopped on native stderr before it could record all exit codes. The runner now treats the native process exit code as authoritative and always captures stderr in the step log. Restore also uses the repository `NuGet.config` explicitly. These are bundle utility corrections, not product changes.

## Linux actual runtime in Docker

Host runtime: Docker Desktop Linux container using `mcr.microsoft.com/dotnet/sdk:10.0`, SDK `10.0.302`, source cloned at exact execution HEAD.

| Command/phase | Result | Evidence | Classification |
|---|---|---|---|
| Restore | 0 | `artifacts/unix-portability/A00/linux-baseline/restore.log` | Linux restore succeeds. |
| Release build | 0; 0 warnings; 0 errors | `artifacts/unix-portability/A00/linux-baseline/build.log` | Clean Linux build. |
| Stable solution tests | Bounded timeout (exit 124) after 30 minutes | `artifacts/unix-portability/A00/linux-baseline/stable-tests.log` | PostgreSQL-dependent Components tests failed because the disposable clone had no `docker-compose.yml` and loopback inside the SDK container was not the Windows host. |
| Unit suite with persistent NuGet cache and PostgreSQL sidecar | 5,181 passed; 116 failed | `artifacts/unix-portability/A00/linux-baseline/test-results/a00-linux-unit-sidecar.trx` | Genuine Linux baseline. Every failed class is assigned in `reviews/06-a00-linux-failure-classification.md`. |
| Previously infrastructure-blocked Components tests | 3 passed; 0 failed | `artifacts/unix-portability/A00/linux-baseline/test-results/a00-linux-components-postgres.trx` | Proves the sidecar setup resolves the missing PostgreSQL prerequisite. |

## macOS

No macOS host or runner is available in this local execution environment. Docker Linux is not macOS evidence. A00 records the platform as unavailable rather than claiming support. Gate C4 remains dependent on an actual macOS runner.

## Known baseline defects carried forward

1. Unit test `Staged_artifact_rejects_oversized_acceptance_append_before_mutation` compares content containing a live UTC second and can fail across a second boundary.
2. A Components test is order-sensitive but passes by itself.
3. The Integration test project can stall before test discovery.
4. Linux database-backed test setup assumes a reachable local PostgreSQL instance or a repository `docker-compose.yml`; the latter is absent.

These defects are baseline facts. They must not be silently waived in later gates: focused changed-area suites remain mandatory, and the post-core stabilization pass must either fix reproducibility or record a bounded external prerequisite.
