# Observed and planned runtime surfaces

Targeted current observations and requested extensions are separate. Availability must be established per actual host, purpose, authority, and registered handler.

## SURF-001

**Name:** Managed HR agent

**Owner module id:** MOD-AGENTS

**Baseline state:** CODE_OBSERVED

**Observed purpose:** InteractiveChat

**Reads:** Agents safe catalog/settings/options, usage, process history; safe CRM search/detail and affiliations.

**Commands:** Draft agent create, allowed settings update, avatar, selected manager review; CRM party create/affiliation upsert.

**Source ids:** SRC-029; SRC-030

**Contract ids:** CON-057; CON-058; CON-059; CON-063

**Limitations:** Identity-bound; separate CRM scope; no Simple Chats methods in the fully read provider; no complete helper/security execution audit.

**Execution status:** NOT_RUN

## SURF-002

**Name:** Managed Scheduler agent

**Owner module id:** MOD-SCHEDULER

**Baseline state:** CODE_OBSERVED

**Observed purpose:** InteractiveChat

**Reads:** Workflow targets and schedule summaries.

**Commands:** Workflow schedule creation.

**Source ids:** SRC-031

**Contract ids:** CON-066; CON-067

**Limitations:** Workflow-only. No implied process schedule, plan update/delete, saved input-JSON disclosure, or headless grant.

**Execution status:** NOT_RUN

## SURF-003

**Name:** Simple Chats definition application API

**Owner module id:** BND-SIMPLECHATS

**Baseline state:** CODE_OBSERVED

**Observed purpose:** Application API, not itself a runtime tool

**Reads:** Definition get/list/paged list.

**Commands:** Create/update/status.

**Source ids:** SRC-034

**Contract ids:** CON-061; CON-062

**Limitations:** Existing changes use expected concurrency token; create idempotency is not promised by the request shape.

**Execution status:** NOT_RUN

## SURF-004

**Name:** HR -> Simple Chats administration

**Owner module id:** BND-SIMPLECHATS

**Baseline state:** REQUIRED_EXTENSION

**Observed purpose:** To be explicitly approved; initially managed interactive integration

**Reads:** Safe definition summaries/settings.

**Commands:** Allowlisted definition create/update/status.

**Source ids:** USER-002; SRC-029; SRC-034

**Contract ids:** CON-061; CON-062

**Limitations:** External adapter only; no transcripts/turns/tools inside Simple Chats; prove atomic create receipt before retries.

**Execution status:** NOT_RUN

## SURF-005

**Name:** General direct Process runtime tool

**Owner module id:** MOD-PROCESSES

**Baseline state:** NOT_DOCUMENTED_AS_EXISTING

**Observed purpose:** None established

**Reads:** No general direct tool inventory established.

**Commands:** No general direct tool inventory established.

**Source ids:** SRC-028

**Contract ids:** CON-028; CON-068

**Limitations:** Retain real HTTP/UI/governed/Structure bridge operations; do not infer tools from process-agent/template names.

**Execution status:** NOT_RUN

## SURF-006

**Name:** Project Structure tools and workflow executor

**Owner module id:** MOD-STRUCTURE

**Baseline state:** BASELINE_CODE_AND_DOCUMENTATION

**Observed purpose:** Existing supported purposes must be reverified

**Reads:** Scoped Structure/project/node reads.

**Commands:** Native/task/asset operations and declared execution bridges.

**Source ids:** SRC-025; SRC-027; SRC-028

**Contract ids:** CON-018; CON-021; CON-023

**Limitations:** Preserve current positive paths; restart/multi-instance receipt guarantees remain explicit proof obligations.

**Execution status:** NOT_RUN

## SURF-007

**Name:** Prompt, workflow, and capability curators

**Owner module id:** MOD-AGENTS

**Baseline state:** DOCUMENTED_NOT_REAUDITED_METHOD_BY_METHOD

**Observed purpose:** Current provider-specific gates require discovery

**Reads:** Safe owner catalog/settings according to each pack.

**Commands:** Owner-specific versioned curation.

**Source ids:** SRC-028

**Contract ids:** CON-034; CON-064; CON-065

**Limitations:** No automatic protected-capability grants or rewriting admitted definitions; each provider independently registered and tested.

**Execution status:** NOT_RUN

## SURF-008

**Name:** General operational agent CRM/Resources lookup

**Owner module id:** MOD-AGENTS

**Baseline state:** REQUIRED_WHERE_MISSING

**Observed purpose:** Explicitly granted task-planning/data-use purpose

**Reads:** CRM identity/availability and Resources/content.

**Commands:** Task write separately uses Work Management.

**Source ids:** USER-002; SRC-032; SRC-036

**Contract ids:** CON-057; CON-069; CON-071; CON-023

**Limitations:** Do not impersonate HR. Source read, provider disclosure, and task audience policy are independent.

**Execution status:** NOT_RUN

## SURF-009

**Name:** Cross-domain workflow owner-operation executors

**Owner module id:** BND-WORKFLOWS

**Baseline state:** TARGET_EXTENSION_PROTOCOL

**Observed purpose:** Verified admitted workflow delegation

**Reads:** Owner queries through registered typed executors.

**Commands:** Only explicitly registered/granted owner commands.

**Source ids:** USER-002; SRC-027

**Contract ids:** CON-072

**Limitations:** Structure executor is the current precedent, not proof every domain executor already exists.

**Execution status:** NOT_RUN

## SURF-010

**Name:** Cross-domain process owner-operation steps

**Owner module id:** MOD-PROCESSES

**Baseline state:** TARGET_EXTENSION_PROTOCOL

**Observed purpose:** Verified admitted process delegation

**Reads:** Owner queries through registered step adapters.

**Commands:** Only explicitly registered/granted owner commands.

**Source ids:** USER-002; SRC-035

**Contract ids:** CON-073

**Limitations:** Preserve checkpoint gap recovery and scope; no browser dependency or generic privileged HTTP calls.

**Execution status:** NOT_RUN

## SURF-011

**Name:** Optional CRM-specialist preset

**Owner module id:** MOD-CRM

**Baseline state:** OPTIONAL_FUTURE

**Observed purpose:** Distinct approved managed identity/purpose

**Reads:** Safe CRM catalogs.

**Commands:** Approved CRM business fields only.

**Source ids:** USER-002

**Contract ids:** CON-057; CON-058; CON-059; CON-060

**Limitations:** Same CRM writer, no new master and no inherited HR technical privileges.

**Execution status:** NOT_RUN
