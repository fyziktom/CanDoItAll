# SB16 Semantic Invariants

- Role executor kind is a typed enum in projection/application contracts, not a persisted or command-facing UI string.
- The role editor panel is a projection renderer and command composer only; it does not know how templates are stored or loaded.
- Template override metadata remains visible and command-carried through template source, snapshot name, snapshot summary, override status, and override summary fields.
- Role command handling rejects stale version tokens for mutating operations instead of silently merging old UI state.
- Step-role binding data is projected as typed responsibility/fallback/rebind metadata and remains separate from launch candidate matching.
- SB16 does not implement launch candidate matching, provisioning, HR recommendation selection, or runtime dispatch.
