# QA Prompt

Validate only the current subbundle.

- For UI work, capture a large-screen browser pass first and then a narrower-width pass.
- Answer the required visual questions:
  - Can I read all texts properly?
  - Is anything overlapping, clipped, or visually colliding?
  - Are components aligned and justified consistently?
  - Are shared components used instead of unnecessary custom wrappers?
  - Are we using available space intentionally?
- For MCP work, capture direct tool proof or harness output, not only source inspection.
- For installer work, prove both publish output and resulting config wiring.
