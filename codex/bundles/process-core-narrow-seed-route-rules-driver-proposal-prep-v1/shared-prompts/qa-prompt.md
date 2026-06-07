# QA / Red-Team Prompt

Attack the implementation for:

- broad Core extraction,
- hidden EF/workspace/storage/AgentFramework dependencies in Core,
- route stage order drift,
- lost route eligibility behavior,
- driver API creep,
- UI/mobile proof drift,
- compatibility wrappers that leak dispatcher-owned models into Core,
- insufficient proof or collapsed subbundle reporting.

Reject the bundle if any of these occur.
