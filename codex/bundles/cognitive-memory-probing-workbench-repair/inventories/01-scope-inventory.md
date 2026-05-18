# Scope Inventory

## Backend Surfaces

| Surface | Current state | Bundle action |
| --- | --- | --- |
| `ICognitiveMemoryProbeService.StartAsync` | Creates active sessions. | Reuse. |
| `ICognitiveMemoryProbeService.AskAsync` | Runs recall and persists turn; returns recall result. | Reuse, expose in UI. |
| `ICognitiveMemoryProbeService.RecordFeedbackAsync` | Persists feedback, finding, optional review, optional regression. | Extend so correction feedback creates review-linked repair candidate. |
| `ICognitiveMemoryReviewUiService.DecideReviewItemAsync` | Applies consolidation candidate review decisions only. | Reuse by creating consolidation candidates for probe corrections. |
| `CognitiveMemoryProbeRegressionTestCaseRecord` | Stores expected evidence text only. | Keep MVP, but improve created expected text and preserve link. |

## UI Surfaces

| Surface | Current state | Bundle action |
| --- | --- | --- |
| `CognitiveMemoryPage` dashboard/review/health tabs | Existing operator page. | Add dialogue workbench tab/panel. |
| Passive probe session panel | Lists sessions only. | Keep as history, add active ask/feedback UI. |
| Recall trace detail | Existing trace panel. | Reuse for selected probe trace where possible. |
| Review decision panel | Existing approve/reject/defer. | Use to approve probe repair review item. |

## Validation Projects

| Project | Project id | Required use |
| --- | --- | --- |
| AI Tap/Faucet | `a845e5c9-43b5-4885-b970-7a63474029c3` | Probe and correction workflow. |
| Curacao Glass factory | `76770384-d515-40ce-9924-78a4a59b4f86` | Probe recall/source visibility workflow. |
