# Original Request

The user ran the web app, created a project, added a research node, and attached a workflow that should process Office365 email into a project-structure markdown summary. The workflow fetched Office365 mail successfully but failed during LLM execution:

```text
Agent context contributor 'cognitive-memory.context' reported failure: Cognitive Memory context requires a project scope.
```

The requested behavior is:

- Fetch a client Office365 email from the configured category.
- Analyze the email with the LLM.
- Add a markdown summary asset node under the project-structure workflow node where the workflow was started.
- Test against the CanDoItAll development database and confirm the workflow runs.

The source email is a Czech Tetris request: static web page, keyboard controls using arrows or W/S/A/D, local best-score persistence, no backend, static hosting, and delivery within one week.
