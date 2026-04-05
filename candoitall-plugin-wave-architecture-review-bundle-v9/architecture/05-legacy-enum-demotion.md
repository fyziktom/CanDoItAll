# Legacy enum demotion
`ProviderKind` and `ResourceKind` may still exist for migration/reporting compatibility, but they must not remain the active identity surface for new/custom plugins.

Rules:
- plugin key is authoritative,
- custom plugin saves must never synthesize a fake enum value,
- editor defaults and summaries must be plugin-key / manifest driven,
- compatibility enums must be nullable or retired once migration completes.
