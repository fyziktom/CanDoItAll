# Implementation Prompt

You are Codex working in `maf-processes-refactor`. Execute subbundles in numeric order. Do not skip critical gates. This is a behavior-preserving runtime refactor only.

Rules:

- Do not create `CanDoItAll.Processes.Core`.
- Do not create production process-driver APIs.
- Do not touch UI/Razor/CSS/JS/TS/image proof files.
- Preserve route order exactly.
- Preserve durable claim semantics exactly.
- Preserve failure transition behavior exactly.
- Keep EF/service-scope side effects in explicitly named stores/coordinators.
- Update the execution report with one row per subbundle.
