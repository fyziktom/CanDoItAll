# Target solution

The target runtime has PostgreSQL as the persisted database provider and InMemory as an explicit development/test override. Legacy catalog entries are handled outside the typed runtime model by a raw JSON quarantine pass that preserves backups and resets active selection safely. Snapshot import/export is not represented as a runtime database profile source.
