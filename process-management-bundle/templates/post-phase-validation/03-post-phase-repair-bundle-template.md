# Post-Implementation Repair Bundle Template

Use this template immediately after finishing `phaseXX`.

## Required Bundle Name

- `post-implementation-bundle-phaseXX`

## Required Repair Subbundles

- `phaseXX-architecture-and-boundary-repair`
- `phaseXX-canonical-model-and-source-of-truth-repair`
- `phaseXX-helper-isolation-and-large-class-repair`
- `phaseXX-persistence-migrations-and-seed-repair`
- `phaseXX-component-first-ui-and-playwright-repair`
- `phaseXX-cross-repo-convergence-repair`

## Required Inputs

- phase execution notes
- failed or weak tests
- Playwright analytics and screenshots
- codeanalytics findings and direct file evidence
- seed-data gaps discovered during tests or UI proof

## Hard Rule

- The next implementation phase may not start until the generated repair bundle passes its readiness gate and its repair subbundles are closed or honestly blocked.
