# Package validation

This record covers the handoff, not .NET or application behavior. The final author-side run checks exact manifest coverage, UTF-8 JSON/Markdown, local links, retained 22-file shared provenance and ZIP integrity including a fresh extraction. It executes helper tests, including rejection of incomplete evidence and static inventory behavior. Actual metrics are in `package-validation.json` and the final response.

`templates/evidence.json` intentionally contains no attempts and only NOT_RUN groups. `--require-complete` must reject it. No synthetic tool fixture is a successful application run. Validators cannot authenticate real-world artifacts or infer test relevance.

The native application, PostgreSQL, Quartz, containers, browser and dotnet watch were not executed in this review. Source findings and reported implementer results remain explicitly distinguished in the review documents.
