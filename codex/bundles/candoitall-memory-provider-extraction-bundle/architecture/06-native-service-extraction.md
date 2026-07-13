# Native Service Extraction

## Extraction strategy

Native extraction should start after generic runtime and MAF replacement are stable. The current native module should be split by ownership:

- domain records and validation into native domain/persistence;
- recall, consolidation, quality, scoring, taxonomy, temporal replay, workspace attention, procedures, signals, and review services into native application;
- Qdrant/RAG projection into optional native projection package;
- professor, curator, and self-regulation agent flows into native MAF integration;
- current native UI into native UI RCL or standalone UI service;
- current host API endpoints into native service API or generic proxy endpoints.

The live native repo exists but is currently unscaffolded. SB24 must create the native solution and project structure in `C:\repositories\CanDoItAll.CognitiveMemory` before any extraction work assumes project paths exist.

## Database extraction

The native service must introduce `CognitiveMemoryDbContext` with `IDbContextFactory<CognitiveMemoryDbContext>`, InMemory and PostgreSQL profiles, migrations owned by the native repo, and no dependency on the host `AppDbContext`. Read-heavy queries should use no-tracking projections where appropriate, and service methods should remain async.

## Compatibility bridge

During transition, the main host may provide a bridge that maps generic memory operations to existing in-process native services. The bridge must be marked temporary, tested, and removed from base composition once the remote native service driver is available.

The bridge must not be the default when no provider is configured. It is valid only behind an explicit temporary provider profile and must be caught by final dependency guards if it survives past its documented retirement point.

## Data migration and retirement

Existing main DB memory tables should not be silently dropped. The final strategy should support one or more of:

- export old native memory rows into native service import format;
- read-only compatibility mode for old installations;
- documented manual migration for development installations;
- final removal from main AppDbContext model and migrations after compatibility acceptance.
