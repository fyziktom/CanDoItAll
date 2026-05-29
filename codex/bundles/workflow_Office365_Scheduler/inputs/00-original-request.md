# Original Request

The user asked to review the pushed implementation and prepare a follow-up bundle. This follow-up must include a subbundle for a new Office365 executor and example template workflows.

Requested scenario:

- Workflow downloads one email where a concrete email address occurs/matches and which is not already marked with a processed category.
- Workflow input includes the concrete email address and the project where it should store either:
  - a summary asset, or
  - task nodes inferred from the email.
- After processing, the workflow must mark the message with a configured category.
- This will often be launched by the Scheduler module, for example every two hours.
- Scheduler setup must allow choosing the email address directly or from CRM, plus a project and optionally a concrete project-structure parent node.
