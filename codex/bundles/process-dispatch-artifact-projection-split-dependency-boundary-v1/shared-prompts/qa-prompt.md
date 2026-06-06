# QA Prompt

Validate that the bundle is pure refactoring.

Questions:
1. Did projection source-family order remain identical?
2. Are all source-family coordinators top-level internal module-local classes?
3. Are side effects still explicit?
4. Does candidate state mutation go through a single helper?
5. Did the bundle avoid Process Core and production driver APIs?
6. Did the bundle avoid UI changes and mobile/small/medium proof?
7. Did every critical gate include build/test/source-scan evidence?
8. Did line-count targets actually improve maintainability rather than just moving code into one new huge file?

Required evidence:
- focused unit tests,
- focused integration tests,
- full solution build,
- anti-stub scan,
- no-core/no-driver scan,
- no-UI/no-viewport proof scan,
- changed-file line counts.