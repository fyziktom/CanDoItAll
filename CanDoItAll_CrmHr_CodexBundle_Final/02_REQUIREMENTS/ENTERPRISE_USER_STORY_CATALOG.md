# Enterprise CRM / HR user-story catalog

This catalog defines the implementation target for a **serious CRM / HR module inside a project-delivery enterprise app**. It intentionally merges CRM, HR, and AI-agent identity around one shared Party root.

Total stories: **120**

Priority convention:

- **High** — required for the first implementation wave set
- **Medium** — useful, but can be layered after the core workflow is stable

All stories below are treated as in-scope unless they are explicitly marked out-of-scope elsewhere in the bundle.

## DIR — Unified directory and shared party foundation

- **DIR-01 [High]** As a business director, I can create one unified party record for a person, organization, organization unit, or AI agent so CRM and HR do not split the same real-world actor across modules. _(Persona: business director)_
- **DIR-02 [High]** As an operations lead, I can classify one party with multiple roles such as customer, partner, employee, contractor, delivery unit, or AI agent owner so the same record can participate in different contexts. _(Persona: operations lead)_
- **DIR-03 [High]** As an account manager, I can search the directory by name, role, tag, status, email, phone, and company so I can find the right record quickly. _(Persona: account manager)_
- **DIR-04 [High]** As a people ops manager, I can archive and reactivate parties without deleting historical references so project, CRM, and HR history stays intact. _(Persona: people ops manager)_
- **DIR-05 [High]** As a sales lead, I can store legal name, display name, preferred name, and external identifiers on a party so the record matches contractual and operational reality. _(Persona: sales lead)_
- **DIR-06 [High]** As a delivery manager, I can register multiple contact methods for one party so teams can see email, phone, messaging, and web contact points in one place. _(Persona: delivery manager)_
- **DIR-07 [High]** As an office manager, I can maintain billing, legal, work, and shipping addresses per party so the app can support normal enterprise operations. _(Persona: office manager)_
- **DIR-08 [High]** As a team lead, I can assign tags and classifications such as region, department, capability, and strategic segment so the registry is filterable. _(Persona: team lead)_
- **DIR-09 [High]** As an org designer, I can create parent-child and peer relationships between organizations and units so the module can represent legal entities and delivery structures. _(Persona: org designer)_
- **DIR-10 [High]** As an HR manager, I can define reporting and membership relationships between people and units so org structure and manager chains are explicit. _(Persona: hr manager)_
- **DIR-11 [High]** As a CRM administrator, I can detect and merge duplicate parties so email history, opportunities, and assignments converge on one source of truth. _(Persona: crm administrator)_
- **DIR-12 [High]** As a data steward, I can import parties from CSV without losing validation feedback so bulk onboarding is practical. _(Persona: data steward)_
- **DIR-13 [High]** As a data steward, I can export filtered party lists so business users can share snapshots or audit the directory. _(Persona: data steward)_
- **DIR-14 [High]** As a project manager, I can see a party activity timeline so I understand the latest interactions, assignments, and changes before acting. _(Persona: project manager)_
- **DIR-15 [High]** As an executive assistant, I can open a party directly from global search so the directory behaves as a first-class application surface. _(Persona: executive assistant)_
- **DIR-16 [High]** As a portfolio manager, I can model a delivery unit as an organization or organization unit instead of an employee so the system supports company-based delivery. _(Persona: portfolio manager)_
- **DIR-17 [High]** As a solution architect, I can attach freeform notes and structured metadata to a party so edge cases do not force schema hacks. _(Persona: solution architect)_
- **DIR-18 [High]** As a compliance lead, I can flag records that contain sensitive data so downstream screens treat them carefully. _(Persona: compliance lead)_
- **DIR-19 [High]** As a support lead, I can see who last changed a party and when so ownership and accountability are visible. _(Persona: support lead)_
- **DIR-20 [High]** As a module owner, I can extend party records with future custom fields without redesigning the entire schema so the module can evolve safely. _(Persona: module owner)_

## CRM — CRM accounts, interactions, and opportunities

- **CRM-01 [High]** As a sales manager, I can create customer and prospect accounts from the unified directory so commercial work starts from the same party model as HR and projects. _(Persona: sales manager)_
- **CRM-02 [High]** As an account manager, I can link multiple contacts and stakeholders to an account so I know who influences delivery and purchasing. _(Persona: account manager)_
- **CRM-03 [High]** As a sales manager, I can set relationship stage such as prospect, active customer, dormant customer, or lost customer so pipeline reporting is meaningful. _(Persona: sales manager)_
- **CRM-04 [High]** As an account executive, I can log meetings, calls, emails, and messages against accounts and contacts so relationship history is preserved. _(Persona: account executive)_
- **CRM-05 [High]** As an account executive, I can capture next actions with owner and due date so follow-up commitments do not disappear. _(Persona: account executive)_
- **CRM-06 [High]** As a sales director, I can maintain an opportunity with stage, value, probability, and expected close date so forecast conversations have structured data. _(Persona: sales director)_
- **CRM-07 [High]** As a pre-sales lead, I can link one opportunity to multiple parties such as customer, partner, internal sponsor, and delivery unit so pursuit structure is explicit. _(Persona: pre-sales lead)_
- **CRM-08 [High]** As a sales director, I can move opportunities through a pipeline so teams have a common operating model. _(Persona: sales director)_
- **CRM-09 [High]** As an account executive, I can record lost reason and competitor context when an opportunity closes unsuccessfully so the business can learn. _(Persona: account executive)_
- **CRM-10 [High]** As an account manager, I can convert a won opportunity into a project context without retyping customer, partner, and delivery unit data so handoff is fast and accurate. _(Persona: account manager)_
- **CRM-11 [High]** As a finance coordinator, I can mark billing contact and contract contact roles on an account so invoicing and approvals go to the right people. _(Persona: finance coordinator)_
- **CRM-12 [High]** As a delivery director, I can see account manager, delivery lead, and sponsor roles on an account so ownership is clear. _(Persona: delivery director)_
- **CRM-13 [High]** As a partnership manager, I can mark partner-sourced opportunities and partner contribution so channel business is visible. _(Persona: partnership manager)_
- **CRM-14 [High]** As a consultant, I can review interaction history before a customer meeting so I enter the conversation with context. _(Persona: consultant)_
- **CRM-15 [High]** As a sales operations analyst, I can filter opportunities by stage, owner, delivery unit, partner, and customer so the pipeline is explorable. _(Persona: sales operations analyst)_
- **CRM-16 [High]** As an account manager, I can maintain renewal and upsell opportunities separately from net-new work so account growth is visible. _(Persona: account manager)_
- **CRM-17 [High]** As a commercial lead, I can store commercial notes, constraints, and timing risks on an opportunity so pursuits are actionable. _(Persona: commercial lead)_
- **CRM-18 [High]** As a business director, I can see account summaries and open opportunities from the CRM/HR home screen so I do not have to reconstruct pipeline from projects. _(Persona: business director)_
- **CRM-19 [High]** As a sales assistant, I can search across opportunities and accounts from one CRM workspace so navigation is fast. _(Persona: sales assistant)_
- **CRM-20 [High]** As a commercial operations lead, I can receive reminders for overdue next actions so opportunities do not stall silently. _(Persona: commercial operations lead)_
- **CRM-21 [High]** As a vendor manager, I can manage partner and vendor organizations in the same registry as customers so external company handling is unified. _(Persona: vendor manager)_
- **CRM-22 [High]** As a delivery manager, I can see primary customer, partner, and sponsor data on project-related surfaces so operational teams stay commercially aware. _(Persona: delivery manager)_
- **CRM-23 [High]** As an account manager, I can convert a prospect account into an active customer without creating a duplicate record so lifecycle changes stay on the same party. _(Persona: account manager)_
- **CRM-24 [High]** As a sales director, I can view stage history and recent movement on opportunities so forecast quality and stagnation are visible. _(Persona: sales director)_

## HR — HR workforce, staffing, recruitment, onboarding, and offboarding

- **HR-01 [High]** As a people ops manager, I can create an employee profile from the unified party model so one person can exist in HR, CRM, and projects at the same time. _(Persona: people ops manager)_
- **HR-02 [High]** As a people ops manager, I can create contractor and freelancer profiles with separate employment metadata so external workforce handling is explicit. _(Persona: people ops manager)_
- **HR-03 [High]** As a delivery director, I can create delivery units and internal teams as parties so staffing can use organizations as well as people. _(Persona: delivery director)_
- **HR-04 [High]** As an HR manager, I can assign manager relationships and home unit membership so org structure is maintained. _(Persona: hr manager)_
- **HR-05 [High]** As an HR manager, I can store start date, end date, employment type, and lifecycle state so workforce records reflect reality. _(Persona: hr manager)_
- **HR-06 [High]** As an HR manager, I can maintain job title, discipline, seniority, and location so staffing data is useful. _(Persona: hr manager)_
- **HR-07 [High]** As a capability lead, I can maintain a person’s skills so staffing and delivery planning can search by competence. _(Persona: capability lead)_
- **HR-08 [High]** As a capability lead, I can record skill proficiency so staffing decisions are not binary. _(Persona: capability lead)_
- **HR-09 [High]** As a capability lead, I can record certifications and important qualifications so regulated or specialized work can find compliant people. _(Persona: capability lead)_
- **HR-10 [High]** As a resource manager, I can record capacity and default weekly availability so bench and load views are grounded. _(Persona: resource manager)_
- **HR-11 [High]** As a resource manager, I can block leave, partial availability, and unavailability windows so plans reflect real capacity. _(Persona: resource manager)_
- **HR-12 [High]** As a resource manager, I can see who is on the bench or nearing availability so I can staff new work. _(Persona: resource manager)_
- **HR-13 [High]** As a project manager, I can request staffing from HR with desired role, skills, dates, and allocation so demand is structured. _(Persona: project manager)_
- **HR-14 [High]** As a resource manager, I can allocate a person or delivery unit to a project with a percentage and dates so staffing commitments are explicit. _(Persona: resource manager)_
- **HR-15 [High]** As a delivery manager, I can see current and future allocations for a person, contractor, or delivery unit so overloads are visible. _(Persona: delivery manager)_
- **HR-16 [High]** As a people ops manager, I can mark a primary delivery unit or home team for a worker so reporting lines and staffing ownership are clear. _(Persona: people ops manager)_
- **HR-17 [High]** As a finance partner, I can store internal cost rate and external billing-rate range for a worker or delivery unit so staffing economics are visible without becoming a payroll system. _(Persona: finance partner)_
- **HR-18 [High]** As a delivery lead, I can search workforce by skill, location, seniority, and availability so I can assemble delivery teams faster. _(Persona: delivery lead)_
- **HR-19 [High]** As a recruiter, I can create a candidate record in the same unified registry so future employees and contractors do not start in a disconnected tool. _(Persona: recruiter)_
- **HR-20 [High]** As a recruiter, I can track candidate stage from sourced to hired or rejected so recruitment progress is visible. _(Persona: recruiter)_
- **HR-21 [High]** As a recruiter, I can schedule interviews and record interview dates so hiring coordination is structured. _(Persona: recruiter)_
- **HR-22 [High]** As a hiring manager, I can capture interview feedback and recommendation so decision quality is documented. _(Persona: hiring manager)_
- **HR-23 [High]** As a people ops manager, I can convert a hired candidate into an employee or contractor profile so recruiting handoff is seamless. _(Persona: people ops manager)_
- **HR-24 [High]** As a people ops manager, I can create onboarding tasks with owner and due date so new joiners do not rely on ad hoc follow-up. _(Persona: people ops manager)_
- **HR-25 [High]** As a people ops manager, I can create offboarding tasks with owner and due date so exits are controlled. _(Persona: people ops manager)_
- **HR-26 [High]** As a mentor coordinator, I can assign manager, buddy, or mentor relationships for onboarding so support roles are visible. _(Persona: mentor coordinator)_
- **HR-27 [High]** As an IT coordinator, I can track access and equipment checklist items during onboarding or offboarding so delivery readiness is observable. _(Persona: it coordinator)_
- **HR-28 [High]** As a people ops manager, I can keep HR-only notes separate from general party notes so sensitive information is handled more carefully. _(Persona: people ops manager)_
- **HR-29 [High]** As a vendor manager, I can represent a subcontractor company and the individual subcontractor separately so commercial and operational relationships are not blurred. _(Persona: vendor manager)_
- **HR-30 [High]** As a delivery director, I can reuse one person as employee, candidate, customer stakeholder, or partner contact when reality requires it so duplication does not grow. _(Persona: delivery director)_
- **HR-31 [High]** As a resource manager, I can assign a company or unit instead of a named person to early staffing placeholders so rough planning can start before individuals are known. _(Persona: resource manager)_
- **HR-32 [High]** As a delivery director, I can see demand versus available capacity by team or delivery unit so staffing risks surface early. _(Persona: delivery director)_
- **HR-33 [High]** As a people ops analyst, I can search for expiring assignments, onboarding items, and contract end dates so HR follow-up becomes proactive. _(Persona: people ops analyst)_
- **HR-34 [High]** As an HR manager, I can reactivate former workers or contractors when they return so historical context is preserved. _(Persona: hr manager)_
- **HR-35 [High]** As a project manager, I can view allocated people and units per project from the HR side so staffing ownership is bidirectional. _(Persona: project manager)_
- **HR-36 [High]** As a capability lead, I can group workers by discipline and capability area so capability health is reviewable. _(Persona: capability lead)_

## AI — AI-agent identity, governance, and provider binding

- **AI-01 [High]** As an AI operations lead, I can register an AI agent as a first-class party so the app can assign and report on agents like people or companies. _(Persona: ai operations lead)_
- **AI-02 [High]** As an AI operations lead, I can link an AI agent profile to a Workspace provider profile and default model so operational configuration is connected to the directory. _(Persona: ai operations lead)_
- **AI-03 [High]** As a solution architect, I can record agent capabilities, limitations, tool access, and scope so assignments are safe and understandable. _(Persona: solution architect)_
- **AI-04 [High]** As a delivery lead, I can assign a human owner or steward to an AI agent so accountability exists. _(Persona: delivery lead)_
- **AI-05 [High]** As a project manager, I can assign an AI agent to a project, work item, or meeting follow-up so blended teams are supported. _(Persona: project manager)_
- **AI-06 [High]** As a quality lead, I can capture validation notes and latest review status for an AI agent so risky agents are visible. _(Persona: quality lead)_
- **AI-07 [High]** As a workspace administrator, I can distinguish local, remote, and third-party agents so infrastructure and risk posture are explicit. _(Persona: workspace administrator)_
- **AI-08 [High]** As a delivery lead, I can search agents in the same directory and assignment flows as people so blended staffing stays unified. _(Persona: delivery lead)_

## PRJ — Project and Workbench integration

- **PRJ-01 [High]** As a project manager, I can assign primary customer, partner, and delivery unit to a project so project context is commercially and operationally complete. _(Persona: project manager)_
- **PRJ-02 [High]** As a project manager, I can assign people, contractors, companies, and AI agents to a project so project staffing is unified. _(Persona: project manager)_
- **PRJ-03 [High]** As a project manager, I can assign responsibility for a project structure delivery node so the workbench shows who is expected to deliver it. _(Persona: project manager)_
- **PRJ-04 [High]** As a project manager, I can choose meeting participants from the unified directory so meetings reference real parties. _(Persona: project manager)_
- **PRJ-05 [High]** As a project manager, I can indicate with whom a meeting happens such as customer, partner, team, or AI agent so the structure reflects real collaboration. _(Persona: project manager)_
- **PRJ-06 [High]** As a project manager, I can assign work items from the unified directory so assignee data is reusable across project and HR views. _(Persona: project manager)_
- **PRJ-07 [High]** As a project manager, I can create a project participant node from an existing party or create a new party from the node flow so local workbench and central registry stay connected. _(Persona: project manager)_
- **PRJ-08 [High]** As a project manager, I can decide whether a participant node is centrally synced or project-local so edge cases do not block work. _(Persona: project manager)_
- **PRJ-09 [High]** As a project manager, I can see related customer, partner, delivery team, and AI agents on project overview screens so context is visible without opening the CRM/HR module first. _(Persona: project manager)_
- **PRJ-10 [High]** As a portfolio manager, I can filter projects by customer, delivery unit, account manager, or key stakeholder so portfolio review is relationship-aware. _(Persona: portfolio manager)_
- **PRJ-11 [High]** As a sales lead, I can convert an opportunity to a project while preserving linked parties and history so handoff does not fragment data. _(Persona: sales lead)_
- **PRJ-12 [High]** As a quality lead, I can link validation runs and test plans to responsible parties so accountability is clear. _(Persona: quality lead)_
- **PRJ-13 [High]** As a resource owner, I can link resources to owning or maintaining parties so operational ownership is visible. _(Persona: resource owner)_
- **PRJ-14 [High]** As a resource manager, I can have project allocations automatically influence HR capacity views so assignments have real staffing impact. _(Persona: resource manager)_
- **PRJ-15 [High]** As a prompt engineer, I can reuse the same AI agent record in prompt, project, and staffing flows so agent identities stay consistent. _(Persona: prompt engineer)_
- **PRJ-16 [High]** As a meeting facilitator, I can pull project-linked parties into meeting defaults so recurring collaboration setup is faster. _(Persona: meeting facilitator)_

## X — Cross-cutting platform, privacy, automation, and QA

- **X-01 [High]** As a platform owner, I can add CRM / HR as a shell module with nested routes so it feels native inside CanDoItAll. _(Persona: platform owner)_
- **X-02 [High]** As a platform owner, I can index parties, interactions, opportunities, workforce records, and agent profiles in global search so the module is discoverable. _(Persona: platform owner)_
- **X-03 [High]** As a platform owner, I can write activity entries for major CRM/HR changes so the timeline reflects relationship work. _(Persona: platform owner)_
- **X-04 [High]** As a UI architect, I can implement the module with BaseLib and standard HTML only so the CRM/HR experience stays outside canvas concerns. _(Persona: ui architect)_
- **X-05 [High]** As a platform owner, I can create and seed the CRM/HR schema automatically on startup so local environments remain simple. _(Persona: platform owner)_
- **X-06 [High]** As a test lead, I can validate the module with unit, component, integration, and Playwright tests so regression risk stays manageable. _(Persona: test lead)_
- **X-07 [High]** As a test lead, I can require screenshot-based semantic review for UI changes so visual issues are not missed by passing tests. _(Persona: test lead)_
- **X-08 [High]** As a platform owner, I can seed default opportunity stages, relationship stages, and other lookup values so the module works immediately after startup. _(Persona: platform owner)_
- **X-09 [High]** As a data steward, I can use archive and safe-delete rules so historical relationships are not broken by aggressive cleanup. _(Persona: data steward)_
- **X-10 [High]** As a compliance lead, I can protect sensitive HR and personal data from overexposure in search and broad list screens so privacy risk is reduced. _(Persona: compliance lead)_
- **X-11 [High]** As a compliance lead, I can audit important CRM/HR changes so the module is reviewable. _(Persona: compliance lead)_
- **X-12 [High]** As a data steward, I can import and export without corrupting duplicate handling or key relationships so bulk operations remain safe. _(Persona: data steward)_
- **X-13 [High]** As a platform owner, I can keep core screens performant with large directories so the module scales beyond toy usage. _(Persona: platform owner)_
- **X-14 [High]** As an architect, I can extend the module with JSON-backed flex fields where appropriate so future requirements do not force schema explosions. _(Persona: architect)_
- **X-15 [High]** As an automation owner, I can trigger reminders and onboarding follow-up jobs from CRM/HR data so the module participates in operational automation. _(Persona: automation owner)_
- **X-16 [High]** As a QA inspector, I can trace every user story to an implementation bundle, validation step, and evidence expectation so execution stays accountable. _(Persona: qa inspector)_
