# Reviewed portability delta

Full proposed-source scan includes untracked files: 5,222 files, 28,670 raw findings in the initial scan. Scanner unit checks: six baseline-enforcer and four secret-artifact cases passed. Raw findings are inventory; only protected executable-source deltas are the enforcement baseline. All six ADDED and seven STALE entries in transcripts/portability-initial.log were reviewed against the source and baseline diff.

| Added fingerprint prefix | Review and corresponding stale entry |
|---|---|
| d54ef3c7b1ea6dde | Razor ExternalWorkspaceRootSelectionField callback now captures renderedSession. Replaces eee12a90dd7fad9c. No filesystem parsing or OS assumption introduced |
| 88b48b5cb4716da9 | ApplyExternalWorkspaceRootSelection gains owner argument and current-session guard. Replaces 69b6e44c6c82dbd6. Existing typed selection/root policy unchanged |
| f7974ab248c7f3cc | Normally constructed AgentEditorAccessQuery receives existing SecretService and maps ListForPickerAsync to Id/Name/KindLabel only. Removes editor injection/use entries 636cf9c1f71b1165 and ec47bd8f0f6f97bf. No secret values, credential export or provider policy change |
| 966471e7bf59b2b8 | Tag distinct comparison moved to AgentEditorDraftPolicy.BuildTags. One occurrence of editor d66efbc0dc77d52d removed. OrdinalIgnoreCase remains deliberate UI tag equality, unrelated to filesystem case |
| b9bcd4f8a08335bb | Tag sort moved with the same UI tag policy. One occurrence of editor 0ca9264a443c6f9c removed |
| da2e1586d91725a1 | Avatar lookup string GUID keys retain OrdinalIgnoreCase in AgentsWorkspaceQuery. Replaces page d9c264889888b0ff. The downstream existing avatar consumer uses textual GUIDs; this does not compare OS paths |

No genuine portability defect was introduced by these deltas. Native/legacy/relative/removed workspace-root behavior and storage selection are covered through actual child/editor tests. Intentional baseline refresh is permitted only from the final complete scan; inspect its diff and require final enforcement without --write-baseline. No scanner pattern, category, or exclusion is weakened.

The final catalog post-await selection guard does not introduce a platform-sensitive API, but source changes still require a fresh scan. Final scan/enforcement transcripts supersede the initial failed enforcement and determine closure.
