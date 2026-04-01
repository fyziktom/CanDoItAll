# Control Plane Schema And Folder Model

## Recommended Profile Record

```json
{
  "id": "0a0a0a0a-1111-2222-3333-444444444444",
  "displayName": "Local SQLite Branch A",
  "providerKind": "Sqlite",
  "sourceKind": "ManagedSqlite",
  "connection": {
    "sqlitePath": "%LOCALAPPDATA%/CanDoItAll/control-plane/database-profiles/managed-sqlite/0a0a.../db/candoitall.db"
  },
  "storage": {
    "mode": "ManagedPerProfile",
    "workspaceRoot": "%LOCALAPPDATA%/CanDoItAll/control-plane/database-profiles/managed-sqlite/0a0a.../workspace"
  },
  "clone": {
    "originProfileId": null,
    "originSnapshotId": null
  },
  "runtime": {
    "fingerprint": "sqlite:managed:0a0a0a0a111122223333444444444444",
    "lockedByRuntimeOverride": false
  },
  "audit": {
    "createdUtc": "2026-04-01T00:00:00Z",
    "lastUsedUtc": "2026-04-01T00:05:00Z",
    "lastSuccessfulOpenUtc": "2026-04-01T00:05:01Z"
  }
}
```

## Recommended Active Profile State

```json
{
  "activeProfileId": "0a0a0a0a-1111-2222-3333-444444444444",
  "lastPromptShownAtUtc": "2026-04-01T00:05:10Z",
  "lastSwitchGeneration": 7
}
```

## Recommended PostgreSQL Secret Storage Pattern

- Store non-sensitive connection metadata in the catalog.
- Store the password or token encrypted by the control-plane secret protector.
- Persist the DataProtection key ring in the control plane so the encrypted control-plane payload survives app restarts.

## Recommended Snapshot Manifest Shape

```json
{
  "snapshotId": "b1b1b1b1-5555-6666-7777-888888888888",
  "sourceProfileId": "0a0a0a0a-1111-2222-3333-444444444444",
  "providerKind": "Sqlite",
  "appSchema": {
    "sqliteMigration": "202604010001_InitialSqlite",
    "postgresMigration": null
  },
  "createdUtc": "2026-04-01T00:10:00Z",
  "tableCount": 39,
  "storageFolders": [
    "managed-files",
    "exports",
    "evidence"
  ],
  "transport": {
    "kind": "Ipfs",
    "cid": "bafy..."
  }
}
```

## Guardrails

- Do not store this control-plane catalog inside `Workspace_Settings`, `Workspace_ProviderProfiles`, or any other selected-app-database table.
- Do not store raw PostgreSQL passwords in plain text.
- Do not reuse the selected-app-database secret tables to open the selected app database.
