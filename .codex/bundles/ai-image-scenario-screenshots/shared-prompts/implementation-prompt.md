# Implementation Prompt

Implement only the active subbundle.

Keep process core generic. Put screenshot, Playwright, app-startup, OpenAI image-generation, route discovery, review, and storage specifics into provider profiles, agent configuration, capabilities, process templates, step descriptions, prompt resources, or tools.

Before editing, verify the prerequisite gate in the subbundle README. After editing, run the exact proof required by that subbundle, update `reviews/01-execution-report.md`, and stop if the progression gate cannot honestly pass.

Use strongly typed C# models and constants for provider/tool metadata. Avoid magic strings except route text, UI text, external protocol names, and process-template JSON keys that are part of the existing pack format.
