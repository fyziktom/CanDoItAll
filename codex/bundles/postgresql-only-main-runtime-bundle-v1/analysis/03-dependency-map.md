# Dependency map

```mermaid
flowchart TD
    A["SQLite EF provider branch"] --> B["Profile/control-plane SQLite kinds"]
    B --> C["SQLite UI actions and dev endpoints"]
    B --> D["SQLite tests and test fixtures"]
    A --> E["SQLite migration project"]
    A --> F["Runtime database switching/drain/lease behavior"]
    F --> G["General PostgreSQL runtime primitives"]
    G --> H["Process/workflow/outbox tuning"]
    B --> I["SQLite-backed snapshot profiles"]
    H --> J["PostgreSQL migration consolidation"]
    I --> J
    J --> K["Final validation"]
```

Key ordering principle:

1. Remove provider/project/dependencies.
2. Remove profile contract and UI/test references.
3. Remove general limitations.
4. Tune process/workflow specifics.
5. Remove/defer snapshot paths.
6. Consolidate PostgreSQL migrations.
7. Validate.
