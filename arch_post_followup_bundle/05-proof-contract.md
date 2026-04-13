# Proof contract

A subbundle is not done until its proof exists in artifacts and the artifacts match the claimed scope.

## Mandatory proof categories

### Build / compile
- `dotnet build CanDoItAll.slnx -v:minimal`

### Integration proof
Fresh `.trx` files must exist for the relevant Process integration suites.

### Component proof
Fresh component `.trx` files must exist for `ProcessWorkspace` and related canvas/editor surfaces after UI/query work.

### MCP proof
Fresh MCP `.trx` proof is required again if the Process MCP surface is touched or closure depends on it.

### Migration proof
Schema-changing phases must regenerate migration scripts for both providers.

## Follow-up-specific proof that must exist before closure

- proof that cyclic/self-loop graphs are rejected;
- proof that duplicate runtime singular rows are rejected;
- proof that pending autosave can no longer race publish/delete/export;
- proof that stale save is rejected in the published-only/no-draft path;
- proof that the workspace still renders correctly after read-model/query cohesion work.

## Reporting rule

For every gate and for final closure, update:

- `reviews/00-execution-report-template.md` or its live counterpart;
- `reviews/01-architecture-gate-memo-log-template.md` or its live counterpart.

Do not mark any gate or final closure as passed until the documents and artifacts agree.
