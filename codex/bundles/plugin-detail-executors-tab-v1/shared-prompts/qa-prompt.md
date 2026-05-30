# QA Prompt

Review `plugin-detail-executors-tab-v1` for literal closure of the raw request.

- Confirm the new tab is a plugin detail tab, not a workflow-authoring tab.
- Confirm executor rows come from the selected plugin descriptor and not hard-coded plugin-specific UI data.
- Confirm each row includes executor name and short description/instruction text.
- Confirm the no-executors case is intentional and tested.
- Confirm desktop and narrow browser proof or a documented browser blocker exists.
- Confirm proof manifest paths exist, transcripts contain exit codes, and raw note closure cites code and test/browser artifacts.
