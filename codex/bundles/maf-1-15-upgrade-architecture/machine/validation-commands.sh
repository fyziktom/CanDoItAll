#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-$(pwd)}"
ROOT="$(cd "$ROOT" && pwd)"
OUT="$ROOT/.artifacts/maf-1.15-validation"
mkdir -p "$OUT"

run_logged() {
  local name="$1"
  shift
  printf 'Running %s\n' "$name"
  "$@" 2>&1 | tee "$OUT/$name.log"
}

cd "$ROOT"

dotnet --info > "$OUT/dotnet-info.txt"
git rev-parse HEAD > "$OUT/git-head.txt"
git status --short > "$OUT/git-status.txt"

run_logged 01-restore-solution dotnet restore CanDoItAll.slnx

MAIN="src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj"
WORKFLOWS="src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj"
HOSTING="src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj"

dotnet list "$MAIN" package --include-transitive > "$OUT/02-package-main.txt"
dotnet list "$WORKFLOWS" package --include-transitive > "$OUT/03-package-workflows.txt"
dotnet list "$HOSTING" package --include-transitive > "$OUT/04-package-hosting.txt"

run_logged 05-build-main-maf \
  dotnet build "$MAIN" --no-restore "-bl:$OUT/05-build-main-maf.binlog"

run_logged 06-build-workflow-adapter \
  dotnet build "$WORKFLOWS" --no-restore "-bl:$OUT/06-build-workflow-adapter.binlog"

run_logged 07-build-hosting \
  dotnet build "$HOSTING" --no-restore "-bl:$OUT/07-build-hosting.binlog"

run_logged 08-build-solution \
  dotnet build CanDoItAll.slnx --no-restore "-bl:$OUT/08-build-solution.binlog"

run_logged 09-test-solution \
  dotnet test CanDoItAll.slnx --no-build \
  --logger "trx;LogFileName=maf-1.15-tests.trx" \
  --results-directory "$OUT"

printf 'MAF 1.15 validation output: %s\n' "$OUT"
