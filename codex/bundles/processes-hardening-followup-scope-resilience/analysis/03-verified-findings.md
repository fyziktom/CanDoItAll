# Verified Findings From `processes-hardening`

## VF01 - Finalizer exists and is useful but incomplete

`ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` defines executor kinds, artifact modes, validation statuses, producer kinds, validation diagnostics, and `FinalizeStepCompletionAsync`.

The direct agent path and workflow-backed path both now call this finalizer. This is a meaningful improvement.

## VF02 - Workflow candidate contract gap

In `LoadDispatchCandidateAsync`, the workflow assignment branch constructs a `DispatchCandidate` with empty expected artifacts and no artifact inputs. Finalizer validation then uses `candidate.ExpectedArtifacts` to decide what required artifacts to validate. Therefore workflow-backed process steps can still complete without their process artifact contract being evaluated.

## VF03 - Subprocess parent finalizer bypass

Subprocess handling still calls `ProjectCompletedSubprocessArtifactsAsync` and then directly transitions the parent step with `TransitionStepWithClaimAsync`. It does not pass through the finalizer.

## VF04 - Subprocess placeholder record can still satisfy an expectation

The subprocess projection creates a parent artifact record with the expectation id even when `sourceArtifact` is null. The provenance explicitly says no child artifact was available. This should become a diagnostic/gap record, not a satisfying artifact.

## VF05 - Artifact validation is overly string-driven

`ResolveArtifactExpectationMode`, `MatchesDeclaredFormat`, and `ContainsPlaceholderArtifactSignal` rely on text tokens and file extensions. This will produce false positives and false negatives in a generic process runtime.

## VF06 - Current-run lineage check is too loose

`IsCurrentRunArtifact` enforces process run and step run, but for most producer kinds it does not require the external reference or provenance to match the current execution run or workflow run. This allows stale same-step artifacts to satisfy new attempts.

## VF07 - Missing upstream materialization can block the downstream path before source repair

`TryRequestMissingUpstreamArtifactMaterializationAsync` moves the downstream step to `Blocked`, then asks the source step to rerun. The visible dispatch candidate query does not include blocked steps, so a follow-up unblock mechanism must be added or proven.

## VF08 - Step boundary is prompt-only

`BuildExecutionPromptCore` tells agents not to execute side actions or mutate external targets unless the current step contract requires it. That is necessary but not sufficient. The runtime should pass a structured operation policy to tools so unauthorized writes/scaffolds/launches are denied.

## VF09 - Branch outcomes should be used more often

The prompt already tells branchable reviews to complete with repair/remediation/rework branches when those represent the governed disposition. The finalizer and completion status logic must enforce that behavior instead of converting every artifact/proof problem into `Blocked`.

## VF10 - Red-team tests are still too software-heavy

Existing tests include good software scenarios, but the runtime needs non-software red-team cases: finance approval, legal decision log, HR hiring screen, operations incident review, research literature triage, etc.
