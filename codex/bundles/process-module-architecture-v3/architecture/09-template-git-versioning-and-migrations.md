# Template Git Versioning And Migrations

Template and Git implementation must also follow `architecture/19-dotnet-performance-guardrails.md`. JSON migration, Git status/diff, and template indexing must be batched, async where I/O-bound, source-generated for known JSON shapes, and resumable.

## Design Intent

Templates and process configuration are file-first, Git-versioned JSON documents with database indexing. JSON is the source of truth. Markdown, Mermaid, compatibility reports, and import envelopes are generated/exported projections with source hashes.

This design supports modular global components, local overrides, global update publication, conflict detection, manual conflict resolution, deterministic schema migrations, and reviewable history.

## Model Concepts

Canonical component shape:

```json
{
  "schemaVersion": "process-template-component/1.0",
  "contentVersion": "1.2.3",
  "key": "role.example",
  "contentHash": "sha256:...",
  "baseRef": null,
  "compatibility": {
    "minRuntimeSchema": "1.0",
    "maxRuntimeSchema": "2.x"
  }
}
```

Template files:

- component JSON files for roles, artifacts, steps, prompts, validations, checklists, branch families, manager profiles, monitoring profiles, and recovery policies,
- process definition JSON files referencing components,
- local override patch JSON files,
- conflict record JSON files,
- migration manifest JSON files,
- generated projection metadata with source hashes.

Database indexes:

- component key,
- component type,
- content version,
- schema version,
- content hash,
- definition usages,
- local overrides,
- conflict state,
- migration status,
- last indexed commit.

## Global Component And Local Override Workflow

1. User assigns a global component into a process.
2. Process stores a component reference plus resolved base hash.
3. User edits a local override; system writes a local patch against the base hash.
4. Global component changes in a future commit.
5. Publish operation computes a three-way merge: old base, new global, local patch.
6. Non-conflicting changes are applied automatically.
7. Conflicts produce conflict records with JSON pointer paths, old base, new global, local value, and suggested resolution options.
8. User resolves conflicts in UI.
9. Resolution writes an updated local patch and records Git commit metadata.

## Migration Workflow

1. Detect schema drift through template index and migration registry.
2. Ensure clean working tree or create a migration branch through Git wrapper.
3. Snapshot current hashes.
4. Apply migrations sequentially from current schema to target schema.
5. Do not skip intermediate migrations.
6. Validate migrated JSON against schema.
7. Recompute content hashes.
8. Regenerate projections only as generated output when configured.
9. Update database index.
10. Commit migration or leave staged for review depending on mode.

If a template was never loaded during earlier app runs, the migration registry still applies every intermediate migration when the repository migration command runs. The recommended operational mode is migrate all templates when schema drift is detected, because a user with many process templates can tolerate an explicit migration window better than hidden partial migration failure.

## Markdown And Mermaid Decision

Markdown and Mermaid are not canonical. They are generated or exported from JSON when needed.

Reasons:

- The UI canvas already represents flow.
- Sidecars can drift from JSON.
- Stored projections increase migration surface area.
- Users who need documentation can generate and save exports intentionally.

If the product keeps generated sidecars for compatibility, each generated file must contain source JSON hash, generator version, generated-at UTC, and schema version. Migrations update generated files only through projection regeneration, not through handwritten semantic edits.

## Git Wrapper Boundary

`CanDoItAll.Git` wraps Git, not Git semantics reimplemented in application code.

Minimum operations:

- status,
- diff,
- add,
- commit,
- branch,
- checkout/switch,
- merge,
- merge abort,
- conflict file listing,
- resolve conflict marker state,
- log,
- show,
- blame where needed for audit,
- worktree cleanliness check.

Security and process constraints:

- all paths are authorized against repository roots,
- no command string concatenation with untrusted paths,
- no secret values in logs,
- conflict content follows sensitivity policy,
- manager change audit compares allowed mutation scopes against Git status/diff.

## Git UI Components

`CanDoItAll.Components.Git` provides reusable UI components:

- status summary,
- changed-file list,
- diff viewer,
- commit form,
- branch selector,
- merge/conflict summary,
- conflict file viewer,
- conflict resolution editor,
- audit result panel,
- migration review panel.

These components are generic. Process-specific screens compose them with process template metadata.

## Invariants

- JSON is canonical source.
- Component content hash changes when semantic JSON changes.
- Local override patches reference a base hash.
- Publish update uses three-way merge semantics.
- Conflict records are explicit and manually resolvable.
- Migrations run sequentially and record migration IDs.
- Git operations go through `CanDoItAll.Git`.
- Database indexes are derived from files, not the source of truth.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Dirty working tree before migration | Stop or create configured migration branch; do not overwrite. |
| Missing intermediate migration | Stop with migration-chain error. |
| Conflict during global publish | Write conflict record and block publish completion for that usage. |
| Projection hash mismatch | Regenerate or report drift; do not treat sidecar as source. |
| Git command fails | Return typed Git error with sanitized command, exit code, and stderr summary. |
| Unauthorized agent file change | Manager audit incident with diff reference and escalation path. |

## Boundary Rules

- Template services can call Git wrapper; runtime cannot manipulate template Git files directly.
- Git UI components do not know Process runtime state.
- Process template screens can compose Git UI components and template conflict models.
- Migrations mutate files only through template services and Git wrapper.
- In v3, pure `Processes.Templates` owns schema, merge, and migration logic. `Processes.Application` composes template operations with `CanDoItAll.Git` when repository actions are needed.
- `CanDoItAll.Git` remains generic and Process-neutral.
- Runtime history and template compatibility closure are linked in `architecture/17-runtime-history-migration-and-readonly-compatibility.md`.

## Test Implications

- Template tests cover schema validation, component refs, overrides, three-way merge, conflict records, migration chain, skipped-version safety, projection hash drift, and index rebuild.
- Git wrapper tests cover path authorization, sanitized logging, command failures, diff/status parsing, merge conflicts, and commit flows.
- UI component tests cover generic status/diff/conflict rendering without Process coupling.
