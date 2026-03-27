# Shared Library Request Workflow

## Rule

After the split exists, shared libraries must be changed from the CanDoItAll side only.

## Workflow

1. A Zyphonote or other app-level task discovers a shared-library gap.
2. The agent does not patch the shared library directly from that repo.
3. The agent creates a request in the correct CanDoItAll shared-library `Requests` folder using `change-request-template.md`.
4. The request includes exact file references, screenshots, acceptance, and why app-local composition is insufficient.
5. A CanDoItAll implementation agent picks up the request during a controlled wave.

## Recommended Future Skill

Create a skill that:

- detects when a task tries to patch shared libraries from another repo
- redirects the work into the CanDoItAll request workflow
- pre-fills the request template with source refs and screenshots

This skill should refuse direct shared-library edits from Zyphonote unless the working repo is CanDoItAll.
