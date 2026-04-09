# Handoff and AgentFramework context

This bundle still treats the AgentFramework overlay as a **future runtime seam**, but the current review adds a stricter stance:

## What must be true now

- actor responsibility is modeled in the process
- handoff order is modeled in the process
- triage or routing decisions are modeled in the process
- eligible pools and staffing fulfillment stay visible through CRM-HR
- approvals, escalation, and decision rights stay canonical in process semantics
- work packets passed to a human or agent should come from a normalized process-native work brief

## What is intentionally deferred

- actual Microsoft Agent Framework executor binding
- deeper parallel multi-agent orchestration
- intelligence-lake consumption of process telemetry and conformance signals

## New hardening from this pass

The previous bundle already prepared the future AI seam, but this pass closes the most important ambiguity:

> **Future agent collaboration must not become a hidden topology beside the process model.**

That means:

- direct production agent-to-agent wiring is not the default collaboration model
- a triage agent may still choose the next role or target, but that choice must be represented as a governed process decision or routing policy
- future AI sessions, logs, and metrics must correlate back to process run, step, and assignment context
- runtime overlays on the process canvas are valuable, but they remain projections rather than canonical state

## Why this matters

When the future agent handoff adapters arrive, they will need a strong business-owned process model underneath them. That process model must already know:

- who owns the process end-to-end
- who the customer is
- what counts as done at an interface
- who is allowed to decide
- what exact work brief or baton packet was handed over
- and how reality deviates from the official model
