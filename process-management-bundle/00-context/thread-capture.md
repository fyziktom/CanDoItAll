# Thread capture

Main requirements captured from the conversation thread:

- implement process management before the intelligence lake
- add it directly into CanDoItAll
- use CanvasLib for interactive process modeling
- keep durable human/AI actor identity in CRM-HR
- support manager-defined role templates handed to HR for fulfillment
- make handoffs explicit and order-aware
- keep SQLite viable while preserving PostgreSQL parity
- treat shared projects as owners of their own processes
- prepare future AgentFramework handoffs without taking hard runtime dependency now
- extend the bundle so it covers real large-enterprise process-management concerns:
  - process owner
  - end-to-end interfaces
  - decision rights
  - exceptions and input quality
  - outcome metrics and waiting time
  - change governance and process literacy
  - paper-vs-reality conformance review
- treat the **modeled process** as the primary graph through which human and agent collaboration is bound together
- allow triage or routing decisions, but keep them visible and governed **inside the process model**
- make the same process visible as an interactive canvas / mindmap / flow diagram so the operator can see what is happening end-to-end
- recheck the whole bundle against the current CanDoItAll repo and the current AgentFramework overlay before AgentFramework is merged later
- avoid dual registries for role templates, agent profiles, providers, or capabilities
- keep future AI sessions, logs, and metrics traceable back to process and business context
