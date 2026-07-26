# 007 — Agent interview and evaluation evidence API

Status: **missing as a canonical cross-module contract**  
Priority: **high**

## Observed contract

CanDoItAll can execute agents, workflows, and processes, and CRM-HR can represent
workforce/recruiting concepts. The pinned public contracts do not provide one canonical
interview/assessment resource that type-links:

- candidate agent and configuration/version;
- prompt-gallery challenge and version;
- agent execution, workflow run, or process run;
- automated checks and rubric scores;
- reviewer decision, approval, and production-readiness status.

The candidate-evaluator example therefore emits a recommendation for a partner-owned
scorecard.

## Needed API

Introduce versioned assessment resources, for example:

```http
POST /api/agent-recruiting/interviews
POST /api/agent-recruiting/interviews/{interviewId}/attempts
POST /api/agent-recruiting/interviews/{interviewId}/reviews
GET  /api/agent-recruiting/interviews/{interviewId}
GET  /api/agent-recruiting/candidates/{agentId}/readiness
```

An attempt should reference exactly one typed execution target:

```json
{
  "target": {
    "kind": "agent-execution-run",
    "id": "uuid"
  },
  "challengeKey": "crm-follow-up-plan",
  "challengeVersion": "3",
  "rubricVersion": "2",
  "inputHash": "sha256",
  "outputHash": "sha256"
}
```

Do not copy mutable run evidence into the interview as untraceable prose; preserve typed
IDs and immutable hashes.

## Required behavior

- support repeated attempts and comparisons over time;
- separate model-generated evaluation from human approval;
- record evaluator agent/provider/model and rubric version;
- prevent readiness from bypassing agent activation authorization;
- expose incomplete/missing evidence explicitly;
- enforce tenant and candidate visibility boundaries.

## Acceptance

A partner can reconstruct why an agent was approved or rejected, compare improvements
across attempts, and prove that production activation had the required human authorization.
