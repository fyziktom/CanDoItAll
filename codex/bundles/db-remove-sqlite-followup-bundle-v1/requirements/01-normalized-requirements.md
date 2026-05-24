# Normalized requirements

- Remove retired provider/source values, connection records, editor fields, and runtime branches from `src`.
- Add raw JSON quarantine for legacy catalog entries before typed deserialization.
- Keep Data Sources focused on PostgreSQL and remove snapshot controls.
- Remove snapshot runtime service/model surface until a future portable import/export feature exists.
- Add residue audit and regression tests.
- Prove PostgreSQL baseline migration and EF model drift state.
- Audit/tune durable runtime concurrency for PostgreSQL.
- Keep the branch focused and produce final proof.
