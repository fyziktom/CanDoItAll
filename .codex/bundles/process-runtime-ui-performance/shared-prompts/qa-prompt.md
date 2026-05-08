# QA Prompt

Validate the completed subbundle against the raw user request and the proof contract.

Check:

- Does active process observation avoid repeated full run detail loading?
- Do existing process runtime tests still pass?
- Was core timing captured before and after repair?
- Was `/processes` validated with Playwright after app startup?
- Does the browser screenshot show a coherent page with no obvious loading deadlock or broken Runs tab?
- Are raw-note closure rows supported by commands, timings, and browser evidence?
