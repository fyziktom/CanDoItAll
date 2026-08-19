#!/usr/bin/env bash
# Capture a stable CanDoItAll baseline without changing repository content.

set -uo pipefail

usage() {
  cat <<'EOF'
Usage: run-baseline.sh --repo-root PATH --output-root PATH [--configuration Release]
EOF
}

repo_root=""
output_root=""
configuration="Release"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo-root)
      repo_root="$2"
      shift 2
      ;;
    --output-root)
      output_root="$2"
      shift 2
      ;;
    --configuration)
      configuration="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "ERROR: unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$repo_root" || -z "$output_root" ]]; then
  usage >&2
  exit 2
fi

repo_root="$(cd "$repo_root" && pwd -P)"
mkdir -p "$output_root"
output_root="$(cd "$output_root" && pwd -P)"

if [[ ! -f "$repo_root/CanDoItAll.slnx" ]]; then
  echo "ERROR: repository root does not contain CanDoItAll.slnx" >&2
  exit 2
fi

status_file="$output_root/step-status.tsv"
summary_file="$output_root/baseline-summary.md"
: > "$status_file"
printf 'step\texit_code\tlog\n' >> "$status_file"

run_step() {
  local step="$1"
  shift
  local log_file="$output_root/${step}.log"
  echo "=== $step ==="
  (
    cd "$repo_root" || exit 2
    "$@"
  ) 2>&1 | tee "$log_file"
  local exit_code=${PIPESTATUS[0]}
  printf '%s\t%s\t%s\n' "$step" "$exit_code" "$(basename "$log_file")" >> "$status_file"
  return "$exit_code"
}

host_os="$(uname -s 2>/dev/null || echo unknown)"
host_release="$(uname -r 2>/dev/null || echo unknown)"
host_arch="$(uname -m 2>/dev/null || echo unknown)"
git_head="$(git -C "$repo_root" rev-parse HEAD 2>/dev/null || echo unknown)"
git_branch="$(git -C "$repo_root" branch --show-current 2>/dev/null || echo detached)"
git_dirty="false"
if [[ -n "$(git -C "$repo_root" status --short 2>/dev/null)" ]]; then
  git_dirty="true"
fi

cat > "$output_root/host-metadata.json" <<EOF
{
  "schema_version": 1,
  "host_os": "${host_os}",
  "host_release": "${host_release}",
  "host_architecture": "${host_arch}",
  "repository_head": "${git_head}",
  "repository_branch": "${git_branch}",
  "repository_dirty": ${git_dirty},
  "configuration": "${configuration}"
}
EOF

failures=0
run_step "dotnet-info" dotnet --info || failures=$((failures + 1))
run_step "git-status" git status --short --branch || failures=$((failures + 1))
run_step "restore" dotnet restore ./CanDoItAll.slnx --configfile ./NuGet.config || failures=$((failures + 1))
run_step "build" dotnet build ./CanDoItAll.slnx -c "$configuration" --no-restore /m:1 || failures=$((failures + 1))
run_step "stable-tests" dotnet test ./CanDoItAll.slnx -c "$configuration" --no-build --filter 'Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined' /m:1 || failures=$((failures + 1))

secret_scanner="$(cd "$(dirname "$0")" && pwd -P)/scan_artifacts_for_secrets.py"
if [[ -f "$secret_scanner" ]]; then
  python3 "$secret_scanner" --root "$output_root" --output "$output_root/secret-scan.json"
  secret_exit=$?
  printf '%s\t%s\t%s\n' "secret-scan" "$secret_exit" "secret-scan.json" >> "$status_file"
  if [[ $secret_exit -ne 0 ]]; then
    failures=$((failures + 1))
  fi
fi

{
  echo "# Baseline summary"
  echo
  echo "- Repository: \`$repo_root\`"
  echo "- Commit: \`$git_head\`"
  echo "- Branch: \`$git_branch\`"
  echo "- Dirty checkout: \`$git_dirty\`"
  echo "- Host: \`$host_os $host_release $host_arch\`"
  echo "- Configuration: \`$configuration\`"
  echo "- Failed steps: $failures"
  echo
  echo "## Step status"
  echo
  echo '| Step | Exit code | Log |'
  echo '|---|---:|---|'
  tail -n +2 "$status_file" | while IFS=$'\t' read -r step exit_code log; do
    printf '| %s | %s | `%s` |\n' "$step" "$exit_code" "$log"
  done
} > "$summary_file"

echo "Baseline evidence: $output_root"
if [[ $failures -ne 0 ]]; then
  echo "RESULT: FAIL ($failures failed step(s))"
  exit 1
fi

echo "RESULT: PASS"
