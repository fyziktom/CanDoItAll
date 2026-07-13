# Normalized Requirements

## Requirements

| ID | Requirement | Observable Success Criteria | Owning Subbundle |
| --- | --- | --- | --- |
| R001 | Remove agent-specific Financial Strategist, margin, document-domain, and project-structure writeback implementation work from this bundle. | Bundle artifacts state those topics as deferred and no subbundle implements them. | SB01 |
| R002 | Produce a repo-grounded responsibility map for `MafAgentRuntime` and related partials. | Map lists current responsibilities, source files, target owners, dependencies, tests, and extraction risk. | SB01 |
| R003 | Define typed runtime contracts and the composition-root strategy before extraction. | Contracts for runtime build, capability composition, provider composition, diagnostics, measurements, and dependency defaults are specified and reviewed. | SB02 |
| R004 | Extract capability access planning, capability composition, runtime provider composition, and provider filtering into directly testable collaborators. | Tests prove collaborator behavior without private reflection or full runtime construction for moved behavior. | SB03 |
| R005 | Extract provider build, runtime session, credential resolution, streaming dispatch, and finalizer coordination behind focused drivers/factories. | Provider/session/finalizer behavior has direct tests and integration parity tests. | SB04 |
| R006 | Extract workspace, MCP, context, skill, storage, and built-in tool helpers into focused feature drivers. | Each moved driver has direct tests with fake dependencies and production registration. | SB05 |
| R007 | Replace unsafe service-locator/fallback construction with explicit DI registration, typed defaults, or explicit optional boundaries. | Required missing services fail predictably; legitimate defaults are registered and tested. | SB02, SB04, SB05 |
| R008 | Improve testability and integration mockability. | New test harness can mock provider clients, runtime tool providers, context contributors, workspace services, MCP clients, diagnostics, and metrics. | SB06 |
| R009 | Reduce reflection-based testing for moved behavior. | Touched behavior has direct collaborator tests; remaining reflection tests are listed with follow-up or justification. | SB06 |
| R010 | Preserve behavior, security, access filtering, approval wrapping, credential masking, disposal, and diagnostics. | Existing relevant unit/integration tests pass and new parity tests prove no behavior loss. | SB03-SB07 |
| R011 | Analyze and validate performance impact of the architecture split. | Baseline and after-change measurements exist for startup, capability composition, provider attachment, descriptor creation, filtering, and external provider boundary. | SB01, SB07 |
| R012 | Keep execution auditable and semantically adequate. | Critical subbundles write proof manifests, semantic invariants, source assertions, anti-stub audits, and execution-report updates. | All |

## Non-Functional Requirements

| ID | Requirement | Validation |
| --- | --- | --- |
| NFR001 | Prefer the smallest staged extraction over a broad rewrite. | Each subbundle has a narrow source scope and explicit do-not-do list. |
| NFR002 | Use strongly typed C# request/result records, options, enums, and interfaces for real seams. | Architecture review and tests reject stringly typed internal contracts where avoidable. |
| NFR003 | Preserve existing public behavior and runtime extension points. | Integration parity tests and source assertions. |
| NFR004 | Improve testability without over-abstracting every private method. | Each abstraction must enable direct tests, mocking, or a real boundary. |
| NFR005 | Separate local runtime performance from external provider latency. | SB07 measurement report. |

## Explicit Non-Goals

- Do not implement this bundle during preparation.
- Do not fix Financial Strategist, MarkItDown, quotation extraction, margin calculation, or project-structure writeback.
- Do not introduce a new runtime parallel to MAF.
- Do not convert every private method to an interface.
- Do not remove all partial files in one pass unless execution proves it is safe and necessary.
- Do not optimize startup through speculative micro-optimizations before measurements.
