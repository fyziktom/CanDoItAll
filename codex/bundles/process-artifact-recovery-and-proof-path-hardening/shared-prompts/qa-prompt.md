# QA Prompt

Validate that:

- managed process output product reads satisfy implementation proof
- dotnet stdout/stderr are not accepted as browser console evidence
- downstream missing upstream artifact input blocks do not trigger same-step retries
- upstream completion reopens blocked dependents waiting on missing upstream artifacts

Use targeted integration tests and the full process dispatch test class.
