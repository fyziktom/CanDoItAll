# Managed agent policy profiles

Agents owns technical identities; each domain owns its operations and policy. Profiles describe possible capability sets, not automatic assignments.

## ACTOR-001

**Name:** HR

**Business policy owner:** MOD-AGENTS

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-057; CON-058; CON-059; CON-060; CON-061; CON-062; CON-063

**Status:** EXISTING_WITH_REQUESTED_EXTENSION

**Restriction:** Existing identity-bound administration retained; Simple Chats adapter is new; staffing writes only under field policy.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-002

**Name:** CRM specialist

**Business policy owner:** MOD-CRM

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-057; CON-058; CON-059; CON-060

**Status:** OPTIONAL_FUTURE

**Restriction:** No technical agent or provider-price administration by default.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-003

**Name:** Scheduler

**Business policy owner:** MOD-SCHEDULER

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-066; CON-067

**Status:** EXISTING_LIMITED_SURFACE

**Restriction:** Current interactive workflow-only search/create; no assumed process targets.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-004

**Name:** Prompt curator

**Business policy owner:** MOD-PROMPTS

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-033; CON-034

**Status:** DOCUMENTED_CURRENT

**Restriction:** Versioned owner curation; no arbitrary execution grants.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-005

**Name:** Workflow curator

**Business policy owner:** BND-WORKFLOWS

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-029; CON-064

**Status:** DOCUMENTED_CURRENT

**Restriction:** Definition and component changes do not rewrite admitted runs.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-006

**Name:** Capability curator

**Business policy owner:** MOD-AGENTS

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-065

**Status:** DOCUMENTED_CURRENT

**Restriction:** Catalog editing is not grant assignment or self-escalation.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-007

**Name:** Process assistant

**Business policy owner:** MOD-PROCESSES

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-027; CON-028; CON-068

**Status:** SURFACE_DISCOVERY_REQUIRED

**Restriction:** A preset/chat context is not evidence of a direct Process tool provider.

**Grants from display name:** No

**Automatic scope expansion:** No

## ACTOR-008

**Name:** General task agent

**Business policy owner:** MOD-AGENTS

**Technical identity owner:** MOD-AGENTS

**Contract ids:** CON-057; CON-069; CON-071; CON-023; CON-024

**Status:** EXPLICIT_LEAST_PRIVILEGE_TARGET

**Restriction:** Safe lookups and Work Management writes; no borrowing managed-HR identity.

**Grants from display name:** No

**Automatic scope expansion:** No
