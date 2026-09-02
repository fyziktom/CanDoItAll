# Branch And Conflict Map

## Canonical branch flow

```text
CanDoItAll/development
          |
          | merge into
          v
CanDoItAll/ui-refactoring
          |
          | integration fixes + proof
          v
CanDoItAll/development
          |
          | after green CI
          v
CanDoItAll/main
```

`ui-refactoring-v2` remains on a separate line and must not enter this graph.

## Merge strategy

Use a regular merge commit:

```bash
git checkout ui-refactoring
git merge --no-ff origin/development
```

Do not rebase the colleague's branch. Do not squash the original five commits before the
integration is reviewed. A merge commit makes the lineage and conflict resolution auditable.

## Expected conflict-resolution policy

### `.gitignore`

Start with current `development`; add `.idea/` if it is still missing. Preserve all current
proof/output exceptions and runtime-template exceptions.

### `global.json`

Use current `development` exactly unless development moved to a newer supported SDK before
execution. Do not restore the original branch's `10.0.204` downgrade.

### `package.json`

Start with current `development`; add:

```json
"watch": "dotnet watch --project ./src/App/CanDoItAll.Web"
```

Keep all newer scripts.

### `src/App/CanDoItAll.Web/Components/App.razor`

Start with current `development`; replace only the obsolete BaseLib font asset:

```text
material-icons.css -> material-symbols.css
```

Preserve all newer components, scripts, bundles, and render modes.

### `PODMAN.md`

Do not keep the stale root document as authoritative. Extract still-valid commands into:

```text
docs/operations/podman-macos-development.md
```

Correct source-repository prerequisites and link it from the current documentation index and
container/installing guides. Remove the stale root file after migration.

## V2 contamination guard

Generate the forbidden set dynamically:

```bash
git rev-list origin/ui-refactoring..origin/ui-refactoring-v2
```

Every returned commit must fail this ancestor test on the integration HEAD:

```bash
git merge-base --is-ancestor <forbidden-sha> HEAD
```

Also require:

```bash
! git merge-base --is-ancestor origin/ui-refactoring-v2 HEAD
```

Run the supplied PowerShell or shell guard:

- before merging development,
- after resolving conflicts,
- before merging to development,
- before merging development to main.

## Reopen triggers

Reopen branch analysis if:

- either UI branch was force-pushed,
- `development` moved after the first merge,
- the original branch gained additional commits,
- v2 was rebased onto the original branch,
- a remote branch alias with `ux-` appears and is not identical to the inspected `ui-` branch.
