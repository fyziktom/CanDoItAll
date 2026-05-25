# Risk To Solution Map

| Risk | Solution |
| --- | --- |
| Unsaved artifact not seen during reactivation | Make materialization reactivation transaction-safe and include pending artifact in the evaluated set. |
| Long lineage truncated in external reference key | Store lineage in typed provenance payload/table and use compact hash keys. |
| Script tools bypass product-mutation boundary | Add script side-effect guard and require product-target permission for scripts that can write/execute against targets. |
| Stale text grounds target aliases | Add typed target grounding records with source authority and promotion rules. |
| JSON/content validation not storage-backed | Add artifact content reader and validate stored bytes. |
| Workflow/subprocess output mapping heuristic | Add explicit adapter mappings and ambiguity blockers. |
| Negative branch hides missing own artifact | Add artifact ownership classification to disposition router. |
| Operation contract magic text | Add persisted typed fields and UI selectors. |
| No-progress resets after restart | Add durable retry ledger and progress delta classifier. |
| Lint advisory by default | Apply strict lint automatically for high-risk definitions and start modes. |
