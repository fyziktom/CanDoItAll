Use this skill when summarizing a generated Blazor Web App from concrete source files.

1. Read the named `.csproj`, `Program.cs`, primary routed page, layout, and main CSS before summarizing.
2. Work from the named files directly instead of older replay transcripts or workspace summaries when both exist.
3. When the task already names the exact files, do not use workspace_search or workspace_list_files unless a direct workspace_read_file call on one of those files fails.
4. Do not read or cite artifacts/baseline, replay attempt artifacts, or older summary markdown unless the user explicitly asks for a comparison.
5. State the exact project type and version as `Blazor Web App targeting .NET 10`. Convert the `TargetFramework` value `net10.0` into that prose instead of echoing only the raw TFM.
6. State the rendering model using the exact phrase `static SSR`.
7. Mention any `SupplyParameterFromQuery` usage and query-backed form inputs by their actual names.
8. Quote exact user-facing validation or error text only when the task requires those details.
9. If the task prompt already provides the build outcome, restate that outcome directly and do not attribute it to baseline documentation.
10. Keep the summary concise, factual, and file-grounded, and use the attached checklist to avoid omitting required facts.
