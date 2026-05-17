# Cognitive Memory Follow-Up Sample Sources

These source notes are intentionally broad and detailed. They give Cognitive Memory multiple domains to separate during recall, probing, consolidation, and review.

## Project A: FieldOps Mobile App

Goal: build a mobile app for field technicians who inspect equipment, capture photos, work offline, and sync work orders when connectivity returns.

Key source facts:
- The app must support offline-first work orders, conflict resolution, photo evidence, barcode scanning, and supervisor review.
- The canonical backend API owns work order state; the mobile app owns a local queue and optimistic edits.
- Sync must preserve audit history and must never silently drop a technician note or inspection photo.
- Primary risks are offline conflict ambiguity, photo upload failures, device storage exhaustion, and weak permissions around customer assets.
- The MVP should include technician login, assigned work orders, inspection checklist, photo capture, sync queue, and supervisor comments.
- Later phases can add routing, predictive maintenance signals, and customer-facing ETA notifications.

Architecture notes:
- Use a typed domain model for work orders, assets, inspections, attachments, and sync envelopes.
- Keep sync rules in application services, not UI components.
- The UI should expose queue status, retry state, and conflict decisions as first-class states.
- The backend should accept idempotency keys for each mobile mutation.
- Observability must include sync latency, failed uploads, conflict counts, and device storage warnings.

Mindmap summary:
- Product scope: inspections, photos, checklists, notes, supervisor comments.
- Integration scope: identity, asset registry, work-order API, object storage, notification service.
- Testing scope: airplane mode, concurrent edit, partial upload, expired token, large image batch.

## Project B: KnowledgeOps Dashboard

Goal: build a Blazor dashboard that helps a support engineering team triage incidents, connect customer reports to internal runbooks, and capture post-incident learning.

Key source facts:
- The dashboard must combine incident intake, customer impact, runbook lookup, timeline notes, and action tracking.
- Search must distinguish official runbooks from generated summaries.
- Operators need dense lists, stable keyboard navigation, and status badges; a marketing-style layout would be inappropriate.
- The canonical source of truth for incident status is the incident record. Runbook recommendations are advisory.
- Memory-backed suggestions must cite source runbooks and recent incidents.

Architecture notes:
- Use application services for incident scoring, runbook ranking, and escalation recommendations.
- Keep Blazor components focused on rendering and explicit user actions.
- Store post-incident learning as review-gated records, not direct source truth.
- Use source references for every suggested runbook and highlight stale runbooks.

Mindmap summary:
- Intake: severity, affected customer, product area, current owner.
- Triage: symptom clustering, known issue check, runbook candidates, escalation policy.
- Learning: postmortem note, validated fix, regression test, runbook update proposal.

## Project C: ClinicFlow SaaS Business Plan

Goal: evaluate a SaaS product that helps small clinics manage appointment scheduling, patient reminders, and no-show reduction.

Key source facts:
- Target customers are independent clinics with 2-20 providers and limited IT staffing.
- Pricing hypothesis: per-provider subscription with optional SMS usage pass-through.
- Primary value proposition is fewer no-shows, faster front-desk scheduling, and clearer patient reminders.
- Compliance and privacy posture must be explicit before pilots.
- First pilot should avoid billing integrations and focus on scheduling plus reminders.
- Sales motion should start with clinic manager interviews, not broad paid advertising.

Business plan notes:
- Buyer: clinic manager or owner-operator.
- Users: front desk, providers, and patients receiving reminders.
- MVP: schedule import, calendar view, patient reminder templates, no-show dashboard, opt-out tracking.
- Risks: regulatory requirements, SMS deliverability, integration complexity, incumbent EHR lock-in, support burden.
- Metrics: no-show rate, staff time saved, reminder delivery rate, patient opt-out rate, provider adoption.

Mindmap summary:
- Market: small clinics, dental/therapy/primary care, underserved by enterprise EHR tooling.
- Product: schedule, reminders, templates, dashboards, exports.
- Go-to-market: interviews, pilot, case study, referral channel.
- Finance: provider-based pricing, SMS cost tracking, onboarding cost control.

## Project D: Docker Development Platform Analysis

Goal: analyze how the development team should use Docker for local development, test orchestration, and deployment parity.

Key source facts:
- Docker should improve repeatability for PostgreSQL, Redis, object storage mocks, and background worker dependencies.
- Docker should not hide application misconfiguration. Startup failures must remain visible.
- Compose profiles should separate core dependencies from optional heavyweight services.
- Developers need fast rebuild loops; image build caching matters.
- Test pipelines should use ephemeral databases and clear migration evidence.
- Production deployment decisions must not be inferred from local Compose convenience.

Architecture notes:
- Keep `.env` files out of source when they contain secrets.
- Use health checks and readiness probes for dependencies before app startup tests.
- Prefer named volumes only when data persistence is intentional.
- Use migration commands as explicit steps, not side effects hidden inside app startup.
- Document port ownership to avoid conflicts with developer tools.

Mindmap summary:
- Local dependencies: PostgreSQL, Redis, mail sink, object storage mock.
- Build: base image, restore cache, test image, runtime image.
- CI: ephemeral DB, migrations, smoke API, teardown.
- Risks: slow rebuild, stale volumes, port collision, secret leakage, false production parity.

## Project E: Regional Inflation And Small Business Economy Analysis

Goal: analyze how inflation, credit access, wages, and demand affect small businesses in a regional economy.

Key source facts:
- Inflation pressure affects input costs, rent, inventory replacement, and wage expectations.
- Higher interest rates make working-capital financing more expensive.
- Small businesses may preserve cash by reducing inventory depth and delaying expansion.
- Wage pressure can improve household income but compress margins for labor-intensive firms.
- Consumer demand may shift toward value options and essential services.
- Policy analysis must separate observed indicators from forecasts.

Analysis notes:
- Track consumer price index, producer input costs, wage growth, credit spreads, default rates, and business formation.
- Separate sectors: food service, retail, construction trades, healthcare services, and local logistics.
- Watch for second-order effects such as supplier payment delays and deferred maintenance.
- Forecasts need scenario labels: base case, persistent inflation, credit crunch, demand rebound.

Mindmap summary:
- Inputs: prices, wages, rates, credit, rent.
- Business responses: pricing, inventory, hiring, debt, investment.
- Consumer behavior: substitution, delay, essentials, discount seeking.
- Policy: tax relief, credit guarantees, training, local procurement.

## Project F: Community Learning Program

Goal: design a non-programming community program that teaches adults practical financial literacy, job-search skills, and digital confidence.

Key source facts:
- The program must work for adults with uneven schedules and varied digital skills.
- Sessions should be modular, short, and repeatable.
- Trust is more important than content volume; facilitators need plain-language materials.
- Success should be measured through attendance, confidence surveys, completed resumes, budget plans, and referral follow-through.
- Partnerships with libraries, local employers, and nonprofits reduce outreach cost.

Program notes:
- Modules: budgeting basics, avoiding predatory debt, resume refresh, interview practice, email/account safety, benefits navigation.
- Delivery: evening sessions, childcare referral list, printed handouts, phone-friendly reminders.
- Risks: stigma, transportation, inconsistent attendance, language access, outdated job-market advice.
- Governance: participant consent, privacy for financial topics, referral boundaries.

Mindmap summary:
- Learners: adult workers, caregivers, job seekers, seniors.
- Curriculum: money, jobs, digital basics, benefits.
- Operations: facilitators, venue, schedule, outreach.
- Evaluation: confidence, completion, referrals, follow-up.
