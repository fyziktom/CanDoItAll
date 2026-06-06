# Architect Self-Review

Decision: proceed with another module-local refactor, not Process Core.

Why:
- Projection coordinators are now nested inside a dispatch partial.
- This is a transitional boundary and should be split/narrowed before Core extraction.
- Future drivers need stable evidence-family vocabulary, not hidden private dispatcher methods.

Risk accepted:
- Still no production driver API.
- Still no Process Core.
