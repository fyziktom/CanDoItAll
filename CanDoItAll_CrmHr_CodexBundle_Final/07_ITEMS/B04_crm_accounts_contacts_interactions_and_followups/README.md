# B04 — CRM accounts, contacts, stakeholders, interaction journal, and follow-ups

## Purpose

Implement account and contact views, stakeholder role handling, interaction logging, account summaries, and overdue next-action workflows on top of the unified party model.

## Dependencies

B01, B02, B03

## Main stories covered

- **CRM-01** As a sales manager, I can create customer and prospect accounts from the unified directory so commercial work starts from the same party model as HR and projects.
- **CRM-02** As an account manager, I can link multiple contacts and stakeholders to an account so I know who influences delivery and purchasing.
- **CRM-03** As a sales manager, I can set relationship stage such as prospect, active customer, dormant customer, or lost customer so pipeline reporting is meaningful.
- **CRM-04** As an account executive, I can log meetings, calls, emails, and messages against accounts and contacts so relationship history is preserved.
- **CRM-05** As an account executive, I can capture next actions with owner and due date so follow-up commitments do not disappear.
- **CRM-11** As a finance coordinator, I can mark billing contact and contract contact roles on an account so invoicing and approvals go to the right people.
- **CRM-12** As a delivery director, I can see account manager, delivery lead, and sponsor roles on an account so ownership is clear.
- **CRM-14** As a consultant, I can review interaction history before a customer meeting so I enter the conversation with context.
- **CRM-17** As a commercial lead, I can store commercial notes, constraints, and timing risks on an opportunity so pursuits are actionable.
- **CRM-20** As a commercial operations lead, I can receive reminders for overdue next actions so opportunities do not stall silently.
- **CRM-21** As a vendor manager, I can manage partner and vendor organizations in the same registry as customers so external company handling is unified.
- **CRM-22** As a delivery manager, I can see primary customer, partner, and sponsor data on project-related surfaces so operational teams stay commercially aware.
- **CRM-23** As an account manager, I can convert a prospect account into an active customer without creating a duplicate record so lifecycle changes stay on the same party.
- **DIR-14** As a project manager, I can see a party activity timeline so I understand the latest interactions, assignments, and changes before acting.

## Main routes

- `/crm-hr/crm`
- `/crm-hr/directory`

## Done when

- Accounts and contacts can be reviewed from CRM routes.
- Interactions persist with participants and next-action ownership.
- Overdue next actions are visible.
- Account detail shows stakeholders and recent interaction history.
