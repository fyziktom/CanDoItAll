# SB01 Proof Manifest

## Scope

Subbundle SB01 covers generic hardening and live proof for the multi-team static-browser delivery process.

## Source Changes And Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | `d75a1b3b32e7a7a37c4385083cbc901c6c6dcea8808ee326c9476f4b1b06c443` |
| `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` | `0867dd5e6141b4401709841c0352899ed3212dbe879d9a02b5a361ca5434d1f9` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs` | `87f659f2c80910ade1baa7d00fc3b0eee90e8d657a46ba459bdb92ab343323cf` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` | `ac0949f71cb135c5841e6dbc0ecb2388520a1b6649f5265f93a5d5c8934f7a86` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs` | `0dd9449ddf72eaeba4a79e71e13e98663c75fe1b72547029bf65ae0b468cab0c` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs` | `953ddcad3b3c926bff528505547f104bc14111cf1ff3db8b756c03fc25b5816c` |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs` | `6f347213019b0276f6f9356b9216fd3f349de4d2088d0d17ddca86627cfb8ef2` |
| `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Staffing.cs` | `8f421f95af6e1eda68e0c82202b03a791ae2c474e8bae6de68772d2fc90dcd38` |
| `repo://Templates/Agents/manifest.json` | `e5572918c752a5816c7702ed6b4650e815eac7c8bd7e605465577b1230b2b427` |
| `repo://Templates/Agents/teams/javascript-delivery/members/javascript-application-developer/instructions.md` | `85878228bfea8ef091d9a31ffaf463de85deaa6f8a609d8ecb0bae9b49b2298a` |
| `repo://Templates/Agents/teams/javascript-delivery/members/javascript-qa-review-lead/instructions.md` | `6c5e64a2559b9f4d7e28d25213a43a58b5c5190544d3bcb0417402dc294146f6` |
| `repo://Templates/Agents/teams/delivery-platform/members/security-reviewer/instructions.md` | `98c68d50d1b31409eca5c6ac50ca9b97fea5c62052be4d0953ec6a5e333f0eb5` |
| `repo://Templates/Agents/teams/delivery-platform/members/release-readiness-manager/instructions.md` | `7ff6a103bb9ccd31c901b1a90cc21141baea0214cc040e7c0a1134f8b622c368` |

## Product And Validation Hashes

| Path | SHA-256 |
| --- | --- |
| `C:\programovani\dotnet-demo\output\index.html` | `ae52c5610597f692d48221379d7d27843343a2214ae68038693bf715a71ca442` |
| `C:\programovani\dotnet-demo\output\app.js` | `1e558db42fb163f1811054a289a449433f2779f2ddd76fa930d1391d747dc7a6` |
| `bundle://proof/SB01/evidence/final-validation-browser-runtime.json` | `4d8403a9e4ff73d95512debd6d5f6d8bc260fd46a42a40679d00a01eacb666fc` |
| `bundle://proof/SB01/evidence/final-tetris-runtime.png` | `b9d5bef6b6080ce2203b680575ca91de11dcfa0f85e78110d57bc0440aac8b77` |

## Production Behavior Artifact Matrix

| Behavior artifact | Producer | Consumer | Lifecycle | Negative or guard proof |
| --- | --- | --- | --- | --- |
| Context workspace scope metadata | Process dispatch metadata builder | MAF runtime context contributors | Created per process-step execution and passed into runtime options | Untrusted execution run ignores scope metadata; trusted process test passes |
| Static-server runner guard | Workspace command plan builder | `workspace_pwsh_run_script` execution path | Evaluated before running a PowerShell helper | Foreground static server script unit test denied |
| JavaScript implementation staffing score | Process launch staffing | Launch plan role assignment | Applied during run launch candidate selection | Direct static-client web test prefers JavaScript developer |
| Repair proof carry-forward | Process automation proof validators | Repair retry and step completion logic | Evaluated per dispatch attempt | Repair deliverable mutation tests pass |
| Release-boundary scaling guidance | Process templates and seeded agents | Security/release/rollout agents | Loaded from agent/process catalog | Agent seed integration test asserts boundary guidance |

## Proof Artifacts

- `bundle://proof/SB01/evidence/run-v6-after-post-release-220749.json`
- `bundle://proof/SB01/evidence/02-project-structure-context-brief.md`
- `bundle://proof/SB01/evidence/RELEASE-APPROVAL-RECORD.md`
- `bundle://proof/SB01/evidence/15-deployment-and-telemetry-watch-log.md`
- `bundle://proof/SB01/evidence/post-release-learning-review.md`
- `bundle://proof/SB01/evidence/final-validation-browser-runtime.json`
- `bundle://proof/SB01/evidence/final-validation-browser-evaluate.json`
- `bundle://proof/SB01/evidence/final-tetris-runtime.png`
- `bundle://proof/SB01/evidence/live-demo-output-evaluation-before-repair.json`
- `bundle://proof/SB01/evidence/live-demo-output-snapshot-before-repair.md`
- `bundle://proof/SB01/evidence/live-demo-output-before-repair.png`

## Anti-Stub Audit

- No CanDoItAll runtime code contains Tetris-specific logic.
- Product output was generated by the live process agents, not manually edited by Codex.
- Browser validation served the output root through a temporary static server and only read files, performance entries, canvas pixels, DOM text, and localStorage.
- The final validation rejects the prior shallow pass where `index.html` loaded `bundle.js` while the real app lived in `app.js`.
