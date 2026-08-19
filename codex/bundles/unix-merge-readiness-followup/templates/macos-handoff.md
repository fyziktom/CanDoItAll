# macOS actual-host validation handoff

## Immutable candidate

- CanDoItAll SHA: `386d8beb6038035f89a9a6961ec017d8213879a5`, branch `unix-adoption`
- Exact build-input manifest: `artifacts/unix-merge-readiness-followup/M08-candidate-source-manifest.json`, SHA-256 `a6fe597d186252e913e88b3896faf571e9ce474ef15a2bb8e6f311a7b817461e`, 3,552 files
- Components dependency: package mode, version `0.1.18`
- FileTools dependency: package mode, version `0.1.18`
- SDK: `10.0.302` with `latestPatch`; record the selected SDK and installed runtimes from the actual host
- Package/source mode: `UseLocalCanDoItAllLibraries=false`; no adjacent repository is an input
- M08 artifact manifest: `artifacts/unix-merge-readiness-followup/M08-artifact-hashes.json`, SHA-256 `8b164654cb1b9e08db96260847468a33fa8fcd000e24b7db5ace8ed2d9db2c4b`
- M08 complete redaction report: SHA-256 `8191c30514e2f150bc3a1149ee5909333ef123e7037774b582a9922689387224`

Transfer the exact candidate working tree together with the two ignored manifests. Do not reconstruct the candidate from `HEAD` alone because the implementation is intentionally uncommitted.

## One actual-host command sequence

Run from an actual macOS arm64 colleague host with Docker Desktop, PowerShell 7, Python 3, .NET 10, and Chromium prerequisites. Set `REPO_ROOT` to the transferred candidate; every build uses package mode.

```bash
set -euo pipefail

: "${REPO_ROOT:?Set REPO_ROOT to the exact transferred candidate}"
cd "$REPO_ROOT"
test "$(git rev-parse HEAD)" = "386d8beb6038035f89a9a6961ec017d8213879a5"
test "$(shasum -a 256 artifacts/unix-merge-readiness-followup/M08-candidate-source-manifest.json | awk '{print $1}')" = "a6fe597d186252e913e88b3896faf571e9ce474ef15a2bb8e6f311a7b817461e"

M09_EVIDENCE_ROOT="$(mktemp -d -t candoitall-m09-evidence.XXXXXX)"
M09_HEADLESS_ROOT="$(mktemp -d -t candoitall-m09-headless.XXXXXX)"
M09_POSTGRES_PASSWORD="$(openssl rand -hex 24)"
M09_POSTGRES_CONTAINER="candoitall-m09-macos-postgres"
trap 'docker rm -f "$M09_POSTGRES_CONTAINER" >/dev/null 2>&1 || true' EXIT

dotnet --info > "$M09_EVIDENCE_ROOT/dotnet-info.txt"
dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=false --nologo
pwsh -NoProfile -File ./tools/Validation/Test-RuntimePortability.ps1 \
  -RepositoryRoot "$REPO_ROOT" \
  -Configuration Release \
  -ResultsDirectory "$M09_EVIDENCE_ROOT/runtime" \
  -BuildStampPath "$M09_EVIDENCE_ROOT/runtime/runtime-portability-build-stamp.json" \
  -BuildOnly

pwsh ./tests/Playwright/CanDoItAll.Tests.Playwright/bin/Release/net10.0/playwright.ps1 install chromium
docker run --detach --rm --name "$M09_POSTGRES_CONTAINER" \
  --publish 127.0.0.1:55432:5432 \
  --env POSTGRES_DB=candoitall_development \
  --env POSTGRES_USER=candoitall \
  --env "POSTGRES_PASSWORD=$M09_POSTGRES_PASSWORD" \
  --health-cmd 'pg_isready -U candoitall -d candoitall_development' \
  --health-interval 2s --health-timeout 3s --health-retries 30 \
  postgres:16-alpine

until test "$(docker inspect --format '{{.State.Health.Status}}' "$M09_POSTGRES_CONTAINER")" = healthy; do sleep 1; done
export CANDOITALL_TESTS_POSTGRES_CONNECTION="Host=127.0.0.1;Port=55432;Database=candoitall_development;Username=candoitall;Password=$M09_POSTGRES_PASSWORD;Include Error Detail=true;Timeout=5;Command Timeout=15"

pwsh -NoProfile -File ./tools/Validation/Test-RuntimePortability.ps1 \
  -RepositoryRoot "$REPO_ROOT" \
  -Configuration Release \
  -ResultsDirectory "$M09_EVIDENCE_ROOT/runtime" \
  -BuildStampPath "$M09_EVIDENCE_ROOT/runtime/runtime-portability-build-stamp.json" \
  -Scope All \
  -SkipBuild

dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj \
  --configuration Release --no-build --no-restore \
  --filter 'FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessPlanMigrationIntegrationTests.Legacy_plan_migration_is_transactional_idempotent_restart_safe_and_reversible' \
  --logger 'trx;LogFileName=macos-process-plan-migration.trx' \
  --results-directory "$M09_EVIDENCE_ROOT/migration"

dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj \
  --configuration Release --no-build --no-restore \
  --filter 'FullyQualifiedName=CanDoItAll.Tests.Unit.Infrastructure.SecretPortabilityTests.Local_user_file_preserves_legacy_payloads_and_restart_continuity|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.LocalWorkspaceProcessHostTests.ExecuteAsync_terminates_descendant_after_parent_exits|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkspaceExecutableLocatorTests.MacOS_contract_uses_exact_executable_names_only|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkspaceExecutableLocatorTests.Unix_actual_host_requires_execute_permission_and_resolves_final_symlink_target|FullyQualifiedName=CanDoItAll.Tests.Unit.Infrastructure.RuntimeHostPlatformCapabilityTests.Launchd_system_daemon_template_requires_explicit_service_identity' \
  --logger 'trx;LogFileName=macos-focused-unit.trx' \
  --results-directory "$M09_EVIDENCE_ROOT/focused"

dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj \
  --configuration Release --no-build --no-restore \
  --filter 'FullyQualifiedName=CanDoItAll.Tests.Integration.ManagerProcessDiscoveryIntegrationTests.Current_host_adapter_reads_complete_identity_for_the_current_process|FullyQualifiedName=CanDoItAll.Tests.Integration.McpExternalToolPortabilityIntegrationTests.Local_stdio_MCP_answers_peer_ping_before_list_and_call_responses' \
  --logger 'trx;LogFileName=macos-focused-integration.trx' \
  --results-directory "$M09_EVIDENCE_ROOT/focused"

plutil -lint ./tools/install/unix/com.candoitall.web.plist.in > "$M09_EVIDENCE_ROOT/launchd-lint.txt"
pwsh -NoProfile -File ./tools/Validation/Test-CorePortabilityHeadless.ps1 \
  -RuntimeIdentifier osx-arm64 \
  -RuntimeProfile MacOsHeadless \
  -OutputRoot "$M09_HEADLESS_ROOT"
cp "$M09_HEADLESS_ROOT"/* "$M09_EVIDENCE_ROOT/"

python3 ./codex/bundles/Unix-portability/scripts/scan_artifacts_for_secrets.py \
  --root "$M09_EVIDENCE_ROOT" \
  --output "$M09_EVIDENCE_ROOT-secret-scan.json" \
  --max-file-bytes 60000000

unset CANDOITALL_TESTS_POSTGRES_CONNECTION M09_POSTGRES_PASSWORD
docker rm -f "$M09_POSTGRES_CONTAINER" >/dev/null
trap - EXIT
```

For every failure, record exactly one classification before editing: `product`, `harness`, `environment`, or `unsupported profile`. Any product change invalidates M08 and requires a new frozen candidate.

## Required focused gates

- package-mode restore/build;
- runtime portability catalog on actual macOS arm64;
- PostgreSQL migration/restart slice;
- two-cycle headless publish/start/restart outside checkout;
- `LocalUserFile` restart and owner permissions;
- process-group parent-exits-first descendant cleanup;
- executable lookup/permission behavior;
- MCP ping-before-response fake server;
- launchd template lint/rendering;
- redaction scan.

## Separate deferred item

- macOS Keychain actual-session CRUD/restart may remain `ActualHostUnverified` if alpha support claims remain headless `LocalUserFile` only.

## Evidence

- Attach the actual-host runtime TRXs, focused TRXs, migration TRX, headless summary/logs, `dotnet-info.txt`, launchd lint output, and complete redaction report.
- Record SHA-256 for every attached artifact and the actual selected SDK/runtime.
- Confirm `uname -m` is `arm64` and the runtime profile is `MacOsHeadless`.
- Do not include the PostgreSQL password, connection string, physical purpose roots, environment dump, or Keychain values.

## Result

- `MACOS GO`
- `MACOS NO-GO — <product|harness|environment|unsupported profile>: <bounded reason>`

Current state: `MACOS NO-GO — environment: actual macOS arm64 colleague execution has not occurred`.
