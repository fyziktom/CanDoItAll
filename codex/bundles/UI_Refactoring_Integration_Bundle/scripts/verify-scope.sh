#!/usr/bin/env bash
set -euo pipefail

repo="${1:?Usage: verify-scope.sh <CanDoItAll-root> [head]}"
head_ref="${2:-HEAD}"
remote="${REMOTE:-origin}"
original_branch="${ORIGINAL_BRANCH:-ui-refactoring}"
forbidden_branch="${FORBIDDEN_BRANCH:-ui-refactoring-v2}"
output_dir="${OUTPUT_DIR:-.artifacts/ui-refactoring-integration/scope}"

repo="$(cd "$repo" && pwd)"
git -C "$repo" fetch "$remote" --prune

original_ref="$remote/$original_branch"
forbidden_ref="$remote/$forbidden_branch"

git -C "$repo" rev-parse --verify "$original_ref" >/dev/null
git -C "$repo" rev-parse --verify "$forbidden_ref" >/dev/null
git -C "$repo" rev-parse --verify "$head_ref" >/dev/null

mapfile -t forbidden_commits < <(git -C "$repo" rev-list "$original_ref..$forbidden_ref")
if [[ "${#forbidden_commits[@]}" -eq 0 ]]; then
  echo "Forbidden branch has no unique commits relative to the original branch; refresh scope analysis." >&2
  exit 2
fi

violations=()
for commit in "${forbidden_commits[@]}"; do
  if git -C "$repo" merge-base --is-ancestor "$commit" "$head_ref"; then
    violations+=("$commit")
  fi
done

forbidden_head_is_ancestor=false
if git -C "$repo" merge-base --is-ancestor "$forbidden_ref" "$head_ref"; then
  forbidden_head_is_ancestor=true
fi

mkdir -p "$repo/$output_dir"
{
  printf '{\n'
  printf '  "checkedAtUtc": "%s",\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf '  "head": "%s",\n' "$(git -C "$repo" rev-parse "$head_ref")"
  printf '  "originalHead": "%s",\n' "$(git -C "$repo" rev-parse "$original_ref")"
  printf '  "forbiddenHead": "%s",\n' "$(git -C "$repo" rev-parse "$forbidden_ref")"
  printf '  "forbiddenUniqueCommitCount": %d,\n' "${#forbidden_commits[@]}"
  printf '  "forbiddenHeadIsAncestor": %s,\n' "$forbidden_head_is_ancestor"
  printf '  "violatingCommits": ['
  sep=""
  for commit in "${violations[@]}"; do
    printf '%s"%s"' "$sep" "$commit"
    sep=", "
  done
  printf ']\n}\n'
} > "$repo/$output_dir/scope-verification.json"

if [[ "$forbidden_head_is_ancestor" == "true" || "${#violations[@]}" -gt 0 ]]; then
  echo "Forbidden ui-refactoring-v2 history is present in $head_ref." >&2
  exit 1
fi

echo "Scope guard passed. Checked ${#forbidden_commits[@]} forbidden commits."
