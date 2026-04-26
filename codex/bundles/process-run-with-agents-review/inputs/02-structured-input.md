# Structured Input

## Problem Statement

`process-run-with-agents-fix` proves a backend deterministic agent-driven process can complete, but it does not yet prove that real users can reliably run, observe, diagnose, and recover the same process from the UI.

## User Intent

- Review UI integration, not just backend service correctness.
- Identify whether process runs can be launched, watched, and interacted with from Blazor UI.
- Analyze how AgentFramework artifacts become process artifacts.
- Define behavior when a required artifact is not delivered.
- Define behavior when an agent crashes, loses context, or stops before doing the job.
- Find critical unhandled crash/failure points.
- Prepare a new bundle with detailed subbundles for later implementation.
- Do not execute implementation.

## Non-Goals

- Do not fix product code in this bundle.
- Do not run browser validation for implementation closure.
- Do not rewrite the completed `process-run-with-agents-fix` bundle.
- Do not weaken governed process completion rules.

## Primary Risk

The backend has several recovery and retry paths, but the UI does not surface them as explicit operator workflows. A process can become blocked, failed, or effectively stranded while the operator has too little actionable state to decide whether to retry, reroute, substitute, approve, or stop.
