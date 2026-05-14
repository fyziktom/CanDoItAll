# Original Request

User report on 2026-05-14:

```text
i connected the office365. it looks good now. 
I tested workflow to get and summary email, but it is missing connectionId. 

RunId: 2c0af1b0-a6a4-4621-a5e9-f92fc0924980
WorkflowId: 372ebf04-5e1f-4688-982c-807996b9b28a
VersionId: 50e3b935-e042-4fbb-a861-db22fd3a55eb
BackendRunId: 2c0af1b0-a6a4-4621-a5e9-f92fc0924980
CreatedAt: kvě 14, 2026 14:13:43
UpdatedAt: kvě 14, 2026 14:13:43

it must fill it automatically from the connection I did in plugin settings via oauth2. 
also when I opened the Run Preview in start dialog it does not displayed option to skip storing result in project structure. It should be generic and work across different workflows same as in gmail one, where I tested it and it worked. 
Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to analyze this and improve it across existing workflows. Identify also other similar cases where skip must be implemented.
```

Follow-up report on 2026-05-14:

```text
great. I killed the running demo. When I Run Preview the workflow for the office365 email. it still does not offer to skip step of writing summary into propject structure. It is also missing to change category of the email when it is processed. It must also check that specific category exists. if not it must create that new category. I marked one email with tag CanDoItAllSummaryTest. It is that openai newsletter email. office plugin can fetch it correctly.

Improve it.
```
