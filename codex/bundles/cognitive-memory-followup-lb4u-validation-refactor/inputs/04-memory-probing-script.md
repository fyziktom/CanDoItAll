# Memory Probing Script

Use these probes during OpenAI and Ollama validation. Record prompt, answer summary, context sources, accepted/rejected review decisions, and follow-up action in the workbook and execution report.

## LB4U-Specific Probes

- What is LB4U, who is it for, and what problem does it solve?
- How does the LB4U button-to-server-to-staff workflow work?
- Which hardware and software components are planned for LB4U?
- What are the installation steps for a customer site?
- Which parts of LB4U are still custom engineering work?
- Which planned pilot or validation sites are mentioned?
- What risks or open questions remain for the first release?

## Business And Planning Probes

- What should a proper business plan contain based on the project materials you have studied?
- Which LB4U source facts support your answer about business-plan structure?
- What marketing activities or launch activities are implied by the LB4U plans?
- Which expenses, procurement items, salaries, or team-cost assumptions should be tracked?
- Which knowledge is LB4U-specific and which knowledge appears reusable for other projects?
- What should a project-memory system remember when a team prepares business plans across several projects?

## Human Feedback Loop

- If an answer is useful and traceable, approve the recommendation or generated memory candidate through the review endpoint.
- If an answer is generic, missing important sources, or overstates unsupported facts, reject or request revision with a precise reason.
- If recall misses loaded evidence, ask memory to study the relevant stage more deeply and rerun consolidation.
- If consolidation proposes generic rules without enough source support, reject them and record why.
- If useful cross-project knowledge appears after repeated evidence, accept it and verify that it remains traceable to source support rather than becoming anonymous model text.

## Required Observations

- Record whether the answer uses raw source provenance.
- Record whether the answer distinguishes LB4U facts from generic planning knowledge.
- Record whether the answer improves after staged consolidation.
- Record whether long prompts or long answers are truncated.
- Record whether Ollama `gptoss20b64k` behaves differently from OpenAI `gpt-5-mini`.
