# Original Request

Date: 2026-05-19

The user asked:

> Use `candoitall-bundle-workflow` to prepare and add templates of workflows in our agents workflow module. We need to have some basic examples for people especially with default plugins like gmail and office365. There must be examples to process emails to get summary, or another to identify and create tasks from email (and add it into project structure of specified project).
>
> Another workflows templates might be for creating of some mermaid grapsh based on some input file, or create summary of some source code file, etc.
>
> It should not be hard coded in code. we must have those templates as own files and just load them.

Literal scope notes:

- "must have examples" applies to email summary and email task creation into project structure.
- "especially with default plugins like gmail and office365" means plugin-backed examples should cover both providers where executor support exists.
- "not hard coded in code" means workflow definitions must live in data/template files and be loaded by the existing template pack, not constructed directly in C#.
