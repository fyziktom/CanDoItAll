# Acceptance Matrix

| Surface | Must Be Proven | Test Layer | Notes |
| --- | --- | --- | --- |
| Active profile resolution | Override vs persisted profile vs auto-provisioned SQLite selection order | Unit + integration | Must cover restart semantics and legacy onboarding |
| Runtime switch coordination | Operation drain, switch gate, active generation, failure rollback | Unit + integration | Missing drain proof blocks UI exposure |
| SQLite profile creation/open/import | Managed local file, external file/import, legacy path, snapshot source | Integration + component | UI should not expose unsupported path combinations silently |
| PostgreSQL profile creation/test/activate | Localhost, Docker-hosted localhost, remote connection metadata, create DB | Integration + browser | Requires real PostgreSQL proof or explicit blocked status |
| Schema parity | SQLite and PostgreSQL both migrate to the same app model | Integration | Must not rely on manual SQLite initializers as normal path |
| Legacy SQLite upgrade | Existing startup-created DB baselines safely into migrations | Integration | Must preserve user data |
| Storage isolation | Different profiles resolve different managed-file/evidence/export roots | Unit + integration | Browser proof should verify managed-file URLs after switch |
| Workbench isolation | Per-profile browser keying and safe stale-route fallback | Unit + component + browser | Must include an artifact route that disappears in the target DB |
| Startup modal & switcher UX | Continue/switch/create flow and active DB visibility | Component + browser | Large-screen screenshots required |
| Clone/snapshot/IPFS | Data + storage clone, local transport, IPFS transport/fake-server behavior | Integration + browser when available | Real-node IPFS proof is optional only if a node is unavailable; fake-server coverage is mandatory |
| Anti-fake validation | Execution report rows, screenshots, and blocked states are honest | Process gate | Missing proof must not be reworded as success |
