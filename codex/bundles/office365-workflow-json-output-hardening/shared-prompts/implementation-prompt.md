# Implementation Prompt

Implement SB01 first. Make the smallest runtime change that causes JSON-required workflow LLM components to request provider-enforced JSON response formatting before model execution. Use the component response schema when present, otherwise request generic JSON. Keep strict post-response validation and do not add JSON extraction, repair, or fallback parsing. Add focused tests in `WorkflowExecutorTests.cs` that prove the response-format options are passed and malformed JSON still fails. Record artifact-backed proof under `proof/SB01/`.

After SB01 closes, execute SB02 against the app at `http://localhost:5032`. Use the existing Office365 category summary workflow and the connected account/category the user says is ready. Capture the live run proof or an exact blocker, then update `reviews/01-execution-report.md`.
