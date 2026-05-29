# 05-markdown-render-and-report-output-executor

## Objective

Implement the planned `markdown.render` executor for reports and user-facing outputs.

## Required work

1. Add typed settings for:
   - template string
   - template workspace path
   - JSON input bindings
   - table rendering
   - evidence table rendering
   - output workspace path
   - append/overwrite
2. Implement safe placeholder replacement and JSON-to-table rendering.
3. Support writing output to workspace file and registering artifact content.
4. Add tests:
   - Markdown from JSON payload.
   - Markdown table from array.
   - output file artifact exists and content is retrievable.
   - missing placeholders fail or render safely based on explicit setting.
5. Add sample local folder summary report workflow.

## Acceptance checklist

- Users can produce a Markdown report without custom code.
- Report output can be saved and opened as workflow artifact.
