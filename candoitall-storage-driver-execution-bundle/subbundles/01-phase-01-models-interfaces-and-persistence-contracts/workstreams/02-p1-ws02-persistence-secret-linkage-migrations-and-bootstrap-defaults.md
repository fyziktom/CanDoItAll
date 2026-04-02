# P1-WS02 Persistence, secret linkage, migrations, and bootstrap defaults

## Objective

Persist the storage catalog, routing rules, secret references, and node-link metadata while preserving bootstrap workspace defaults and active-profile behavior.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-002 | Storage configuration defaults | Infrastructure | In scope | Keep bootstrap workspace defaults but add persistent storage catalog/routing configuration in app data. | Migrations + unit tests |
| TP-022 | FTP resource metadata | Resources UI/Domain | Adjacent/in scope | Reuse editor-field ideas only; keep storage catalog separate from project resources or add an explicit bridge. | Design review |
| TP-024 | Secret service | Security | In scope | Link storage records to secret records instead of embedding passwords/tokens in plain config. | Unit tests + migration |
| TP-025 | Project object types | Shared Model | In scope | Add storage-system subtype strategy (prefer Infrastructure subtype or justified new type) and document why. | Design review + Playwright |
| TP-036 | SQLite model snapshot | Migrations | In scope | Add migration and snapshot updates for any new storage entities/columns. | Build + migration diff review |
| TP-037 | PostgreSQL model snapshot | Migrations | In scope | Add matching migration and snapshot updates. | Build + migration diff review |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.Sqlite/Migrations/AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs

## Ordered Implementation Tasks

1. Add app-db entities for storage catalog entries, route/default rules, and any needed storage-object or node-link persistence.
2. Link credentials through secret records instead of embedding them in plain text config.
3. Define migration/update plan for both SQLite and PostgreSQL migration projects.
4. Seed or bootstrap one default local filesystem storage record from the current workspace root to preserve existing behavior after upgrade.

## Acceptance Checklist

- No provider credential is stored unencrypted in application tables.
- Both migration projects stay in sync for all new tables/columns.
- Existing workspaces can upgrade without losing access to current filesystem-managed assets.

## Proof Required

- Update `reviews/01-execution-report.md` with this workstream's command output or browser evidence.
- Add or update automated tests if the task changes executable behavior.
- If the task affects a UI surface, attach both desktop and narrow screenshot paths plus written findings.
- If anything is blocked, record the blocker explicitly instead of downgrading the requirement silently.

## Reopen Triggers

- A workbook touchpoint owned by this workstream has no implementation note, proof route, or linked evidence.
- Any required test command fails or is skipped.
- Any screenshot reveals clipping, overlap, overflow, inaccessible wizard navigation, or incorrect enabled/disabled actions.
- A provider is marked supported without a real protocol-backed validation path.

## Suggested Codex Prompt

```text
Implement workstream P1-WS02 only.

Objective:
Persist the storage catalog, routing rules, secret references, and node-link metadata while preserving bootstrap workspace defaults and active-profile behavior.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/01-phase-01-models-interfaces-and-persistence-contracts/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.Sqlite/Migrations/AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

