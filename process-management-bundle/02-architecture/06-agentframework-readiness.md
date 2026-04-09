# AgentFramework readiness

The uploaded AgentFramework overlay continues to be a future seam, not a first-wave dependency.

## What the process module should prepare now

- actor execution modes: manual / human / AI / hybrid
- explicit approval and escalation vocabulary
- observation and governance vocabulary
- staffing-template snapshots
- interface and handoff payload semantics
- process-native work briefs and baton artifacts
- future external executor correlation ids
- decision-right rules that can later map to agent permission policies

## What the process module should not do now

- take a compile-time dependency on AgentFramework projects
- move durable identity ownership out of CRM-HR
- create a second permanent provider or capability registry
- hide future agent-to-agent routing outside the modeled process
- delay process management on unresolved runtime wrapper questions

## Convergence rules for the future bridge

| Concern | Canonical owner now | Future bridge behavior |
|---|---|---|
| Business role / agent templates | CRM-HR | Runtime may consume snapshots or hints, but does not become the template system |
| Durable AI identity | CRM-HR `AiAgentProfile` | Runtime binds through CRM-HR-owned identity |
| Provider profiles | Workspace | Runtime uses shared provider truth |
| Capability proof ownership | Shared CanDoItAll policy / registry seam | Runtime may consume proof state, not redefine the source of truth |
| Process topology | Processes | Runtime executes within process-defined handoffs and routing |
| Sessions / logs / metrics | External runtime + process correlation records | Must remain attributable to `ProcessRun` / `ProcessStepRun` / assignment context |
| Permission envelope | Process governance + future executor policy | Future runtime policy is narrowed by process step governance, not the other way around |

## Why this is necessary

The uploaded AgentFramework repo already contains temporary runtime-side templates, providers, capabilities, sessions, logs, and metrics.  
That is useful for research, but dangerous as a permanent production shape unless the ownership boundaries above are fixed now.
