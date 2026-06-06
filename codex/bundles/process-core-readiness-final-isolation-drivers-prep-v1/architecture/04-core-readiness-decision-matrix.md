# Core Readiness Decision Matrix

| Area | Current state | Readiness | Next action |
| --- | --- | --- | --- |
| Route stage order | explicit route pipeline and handlers | high | keep module-local now; can become Core candidate after contracts |
| Route kind classification | mostly pure rules | high | prepare contracts later |
| Subprocess lifecycle status mapping | pure rule family | high | prepare Core candidate later |
| Transition request shaping | partly pure, partly claim/application-bound | medium | split pure request shaping from claim side effects |
| Candidate hydration | EF/workspace/agent heavy | low | keep application-local |
| Claim lifecycle | EF/lease/heartbeat heavy | low | keep application-local |
| Agent execution | AgentFramework integration | low | keep application-local |
| Artifact projection | storage/file IO heavy | low | keep application-local |
| Artifact validation rules | partially pure | medium | continue rule extraction; later review |
| Finalizer context mapping | partially pure | medium | isolate models before Core |
| Driver readiness | docs only | not a production API | define only after Core contracts stabilize |

## Decision for this bundle

Do not create Process Core.
The bundle should end with a go/no-go recommendation for the **next** bundle, not with a Core split.
