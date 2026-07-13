# Normalized Requirements

## Functional Requirements

| Id | Requirement |
|---|---|
| R1 | Prepare a new implementation-ready bundle only; do not change production code during bundle preparation. |
| R2 | Refactor `AgentFrameworkProcessExecutionAdapter` into smaller top-level responsibilities with clear contracts and focused tests. |
| R3 | Treat the current adapter partial-class cluster as temporary debt, not acceptable final architecture. |
| R4 | Keep generic process runtime, dispatcher, and MAF receipt infrastructure domain-free. |
| R5 | Move .NET/software-delivery lifecycle, tool-plan, and receipt classification behavior behind process-driver or domain policy seams. |
| R6 | Extract completion gate evaluation and receipt matching into testable services that aggregate issues and support branch applicability. |
| R7 | Extract managed artifact materialization/acceptance/readback behavior into a testable service. |
| R8 | Extract subprocess state resolution, parent bridge behavior, and child root-cause propagation into testable services. |
| R9 | Extract recovery classification and diagnostic-specific rework packet generation into testable services. |
| R10 | Preserve GPTPro root-cause fixes: branch-aware gates, branch-routable completion issues, safe/idempotent retry routing, typed tool plans, resolved tool-critical placeholders, complete diagnostics, and template/agent contract hardening. |
| R11 | Analyze all relevant process templates and artifact templates for the same escalation/root-cause pattern, not only the currently blocked example. |
| R12 | Require C# architecture proof: boundary map, dependency direction, pattern selection, testability plan, partial-class policy proof, CodeAnalytics before/after evidence, and architecture review gate. |

## Non-Functional Requirements

| Id | Requirement |
|---|---|
| N1 | Strongly typed contracts are required for identifiers, keys, driver ids, receipt expectations, issue codes, and route decisions where they cross service boundaries. |
| N2 | Avoid stringly typed logic except unavoidable external protocol/tool names and UI/prompt text. Tool names must be wrapped in typed expectation/descriptor records before used by generic algorithms. |
| N3 | No silent fallback mechanisms. Unsupported driver/policy/receipt cases must fail predictably with actionable diagnostics. |
| N4 | Keep changes scoped and incremental. Do not introduce broad `Common`, `Helper`, `Manager`, or service-locator abstractions. |
| N5 | Add abstractions only when they enable a real boundary, testing seam, or multiple implementation path. |
| N6 | Tests must prove behavior, not just DI resolution or non-null outputs. |
| N7 | Logs/diagnostics created by implementation must include actionable state and avoid sensitive data. |

## Implementation Readiness Requirements

| Id | Requirement |
|---|---|
| IR1 | Every architecture subbundle must include C# architecture impact, boundary ownership, dependency direction, pattern decision, testability contract, partial class policy, and architecture proof sections. |
| IR2 | Every critical subbundle must define failing-first or characterization tests before behavior movement. |
| IR3 | Every extracted service must have direct unit tests that do not instantiate `AgentFrameworkProcessExecutionAdapter`. |
| IR4 | Final closure requires a source assertion that no new adapter partial files were added. |
| IR5 | Final closure requires `rg` assertions for forbidden domain terms in generic runtime/dispatcher/MAF files, with documented exceptions only for true tool protocol/catalog ownership. |

