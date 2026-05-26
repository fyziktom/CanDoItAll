# Structured Input

| Raw note | Exact wording | Normalized requirements | Owning subbundles | Planned proof |
| --- | --- | --- | --- | --- |
| F01 | Potential compile breaker: `ProcessRuntimeViewModels.cs` references `ProcessStepRecoveryOption.None`, while `ProcessDefinitionEnums.cs` currently shows `ProcessStepRecoveryOption` without `None`. | RQ01 | SB01 | Failing build or source assertion, enum/read-model fix, build and targeted regression proof. |
| F02 | Several Blazor template steps still grant `MutateProductTarget` / `ExternalProductTargetMutable` to review, revalidation, writeback, or escalation-style steps where product mutation is not appropriate. | RQ02, RQ03, RQ04 | SB03, SB04, SB05, SB15 | Template audit, negative tests for forbidden mutation, template validation, UI preflight proof. |
| F03 | Non-Blazor templates remain behind the new typed operation-contract model. | RQ02 | SB03, SB08, SB14 | Manifest-wide template contract audit and migration tests. |
| F04 | The Processes API skill exists, but it is still too shallow for the new governance model. | RQ05 | SB02, SB13 | API/tool round-trip tests, skill/docs source assertions, examples. |
| F05 | Project-structure writeback tools appear in process template instructions, but the generic tool policy registration/enforcement surface does not visibly classify `project_structure_*` mutation tools. | RQ06 | SB07 | Tool policy red-team tests and source assertions. |
| F06 | Manual/API step transitions still need proof that they use finalizer-grade artifact validation, not a lighter kind/title/trust check. | RQ07 | SB10, SB11, SB12 | Shared validator implementation, failing-first weak artifact transition test, passing finalizer-equivalent proof. |
| F07 | Template pack metadata still says software process template pack while the pack contains non-software templates. | RQ02, RQ09 | SB08, SB13, SB16 | Template metadata/docs audit and final red-team closure. |
