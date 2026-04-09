# Overview and principles

The process-management module should be a **business-owned canonical module** inside CanDoItAll.

This revision broadens the module from "diagram + runtime + operating model" into **diagram + runtime + operating model + canonical orchestration**. In other words, it must know not only how a process is drawn and executed, but also:

- who owns it end-to-end
- who the customer is
- what interfaces exist between processes
- which decisions are allowed to whom
- what counts as a valid input
- how long work waits compared with how long it is actually worked
- how reality diverges from the paper model
- what exact work brief or baton packet was handed to the next actor
- and how future external runtimes remain subordinate to process and business truth

## Core principles

1. **Processes follow value flow, not org charts.**  
   CRM-HR remains the identity source, but the process graph is not a copy of reporting lines.

2. **Ownership must be explicit.**  
   Governed processes must have process owner, customer, criticality, and value statement before publish.

3. **Interfaces matter as much as steps.**  
   Upstream/downstream boundaries, handoff payloads, and definitions of done are canonical.

4. **Exceptions are part of the design, not a footnote.**  
   Variants, exception paths, decision rights, and input-quality rules are first-class.

5. **Flow telemetry beats activity counts.**  
   Lead time, wait time, rework, and customer outcome matter more than raw task volume.

6. **Reality must be reviewable.**  
   Process conformance compares the model against observed execution and governed field observations.

7. **Governance should be proportional to risk.**  
   The system must support control discipline without turning every path into bureaucracy.

8. **The process is the canonical collaboration graph.**  
   Future human and AI collaboration topology belongs in process definitions, handoffs, work briefs, and governed routing decisions.

9. **External runtimes are correlated, not sovereign.**  
   Future AgentFramework sessions, logs, metrics, and approvals remain attributable to process context and do not become a second canonical workflow layer.

10. **Project context and process orchestration stay separate but linked.**  
    Projects own scope and delivery context. Processes own collaboration and handoff orchestration. Typed references bridge the two.
