# Cognitive Memory PostgreSQL Smoke Source Documents

These documents are intentionally broad and source-like. They are not unit-test fixtures. The loader in this folder creates projects and project-structure nodes through the HTTP API, then asks Cognitive Memory to ingest and consolidate them.

## NimbusFlow Field Service App

NimbusFlow is a .NET and Blazor field-service application for regional maintenance teams. The project assumes technicians often work offline in warehouses, rooftops, basements, and rural customer sites. The architecture therefore treats offline-first capture, sync conflict review, and auditability as primary requirements, not later polish. The first release has three user groups: dispatcher, field technician, and operations manager.

The application is split into a Blazor Web App shell, an application-service layer for work-order orchestration, a domain layer for scheduling, inventory, assets, and inspections, and an infrastructure layer for PostgreSQL, object storage, notification providers, and external ERP imports. Mobile clients use the same HTTP contracts but cache work packages locally. Server-side rendering is allowed for operational dashboards, but field capture screens must degrade cleanly when connectivity drops.

Critical decisions:

- Work orders have immutable event history and mutable projections.
- Offline clients submit command batches with idempotency keys and device timestamps.
- Conflict resolution is explicit: automatic merge is allowed only for non-overlapping form fields; asset status, part consumption, and safety sign-off conflicts require human review.
- Attachments are source evidence. Generated summaries may reference attachments but cannot replace them.
- Dispatch optimization is a recommendation engine; dispatchers retain decision authority in V1.

Risks:

- Sync latency can hide safety-critical updates.
- Technicians may upload photos that include customer private information.
- ERP inventory counts may lag the field app by hours.
- Blazor component state can become difficult to test if workflow rules leak into components.
- Offline databases need retention and encryption policies before pilot.

## LedgerLift SaaS Business Plan

LedgerLift is a proposed SaaS for small accounting firms that manage recurring close checklists for multiple clients. The plan targets five-to-fifty-person firms that have outgrown spreadsheets but do not need a full enterprise consolidation platform. The initial wedge is month-end close coordination with evidence collection, exception tracking, and client-visible status summaries.

Market assumptions:

- The buyer is usually a partner or operations lead, but daily users are staff accountants and client controllers.
- The strongest pain appears in firms that manage more than twenty monthly close clients with recurring document requests.
- Spreadsheets remain the main competitor because they are flexible and already trusted.
- Integrations with QuickBooks Online and Xero matter, but the first purchasing decision is driven by workflow clarity and client accountability.

Product packaging:

- Starter: up to ten clients, checklist templates, document requests, and status dashboard.
- Growth: up to fifty clients, reusable exceptions, role assignments, client portal, and audit export.
- Firm: unlimited clients, advanced permissions, API access, SSO, and custom retention policies.

Open questions:

- Whether evidence retention should be priced by client, storage, or compliance tier.
- Whether client portal users should be billable seats.
- Whether SOC 2 Type I is required before the first paid pilots.
- Whether AI summarization is a differentiator or a compliance objection.

## Docker Delivery Platform Analysis

This project analyzes a developer platform that standardizes local Docker Compose, CI image builds, staging deployments, and production promotion. The platform supports .NET web apps, background workers, PostgreSQL, Redis, and optional Qdrant services. The objective is to reduce environment drift while keeping deployment failures diagnosable.

Architecture principles:

- Images are built once per commit and promoted by digest.
- Compose files are local-development contracts, not production orchestration manifests.
- PostgreSQL development databases are named per task or branch when behavior smoke tests need isolation.
- Secrets never live in Compose files or image layers; local developer secrets are injected from user profiles or secure stores.
- Health checks must validate application readiness, database migration state, and dependent service reachability.

Decision areas:

- Whether to use Docker Compose profiles for optional services such as Qdrant and MinIO.
- Whether local test databases should be created through app development endpoints or direct administrative scripts.
- Whether integration tests should run against containers in CI or against provider-specific test hosts.
- Whether projection workers should run as app-hosted background services or as separate worker containers.

Risks:

- Compose volumes can preserve stale schemas across implementation branches.
- Image layer caching can hide package restore failures until CI.
- Running all optional services locally can create a slow feedback loop.
- Production parity is valuable, but local developer loops still need fast relational-only smoke profiles.

## Regional Inflation And Labor Market Brief

This non-programming research project studies how regional inflation, wage growth, housing costs, and small-business hiring constraints interact in a mid-sized metropolitan area. The intended output is an executive brief for a local economic-development board.

The analysis separates four layers:

- Household pressure: rent, utilities, food, transportation, childcare, and medical out-of-pocket costs.
- Employer pressure: wage expectations, input costs, credit availability, and demand uncertainty.
- Public policy levers: zoning, workforce training, transit access, permitting, and small-business grants.
- Measurement limitations: lagging indicators, survey bias, changes in labor-force participation, and neighborhood-level variation.

Working hypotheses:

- Wage growth can coexist with lower real purchasing power when rent and insurance rise faster.
- Small businesses face a narrower margin for wage competition than regional anchor employers.
- Worker availability is constrained by transit and childcare as much as by advertised wages.
- Aggregate inflation hides different lived experiences across renters, homeowners, retirees, and recent graduates.

The brief must not turn correlation into causation without source evidence. Recommendations should distinguish immediate relief, medium-term capacity building, and structural reforms.

## Urban Food Cooperative Operations Plan

This project designs a member-owned urban food cooperative with a small retail storefront, community-supported agriculture pickup, prepared-food partnerships, and nutrition education programs. The plan is operational rather than software-focused.

Operating model:

- Members buy a refundable ownership share and receive voting rights.
- Non-members may shop, but members receive discounts and priority for limited CSA allocation.
- The storefront carries staple produce, dry goods, culturally relevant pantry items, and prepared-food partner products.
- Volunteer shifts reduce operating cost but cannot be required for access to essential food.
- Supplier relationships prioritize regional farms, minority-owned producers, and transparent labor practices.

Governance decisions:

- Board seats are elected by members with reserved advisory seats for neighborhood partners.
- Food access pricing requires a subsidy fund that is separate from operating cash.
- Spoilage, shrink, and donation policies must be measured weekly.
- Prepared-food partnerships need allergen, labeling, and temperature-control rules.

Risks:

- Member enthusiasm may not translate into reliable volunteer coverage.
- Low-margin grocery operations can fail despite strong community support.
- Subsidized pricing can become financially opaque without clear reporting.
- Supplier resilience matters during weather, transportation, and seasonal shocks.

## Evidence Expectations For Smoke Testing

Cognitive Memory should ingest these documents as source-backed project structure. The smoke should prove:

- project-scoped source ingestion works from detailed mindmap-like project structures;
- consolidation creates durable records, review items, and traceable source links without treating generated summaries as source truth;
- recall and probing either work or return explicit provider-unavailable errors when semantic/RAG dependencies are not configured;
- probe feedback creates review/regression/calibration side effects instead of direct truth mutation;
- self-regulation and answer gates can produce conservative posture decisions from source sufficiency, risk, and calibration inputs;
- professor review produces governed critique/actions without direct canonical mutation;
- Epistemic Drive creates approval-gated learning proposals;
- cross-project promotion is review/policy gated;
- distributed worker jobs require leases, input hashes, algorithm versions, and schema validation before acceptance.
