# Final release gate

## Precondition

All subbundles SB00–SB10 and checkpoints CP0–CP2 are complete.

## Required proof

1. bundle structure and test-policy validators;
2. architecture boundary guard;
3. Release solution restore/build;
4. one stable filtered solution test run;
5. focused HTTP/PostgreSQL test;
6. migration bootstrap test;
7. `dotnet ef migrations has-pending-model-changes`;
8. documentation validation;
9. clean source scan for secrets and absolute developer paths;
10. current branch/commit and changed-file inventory;
11. final architecture review;
12. explicit residual-risk register.

## Not part of this gate

- Playwright;
- UI component tests;
- external provider live calls;
- LiveProcess or LongRunning lanes;
- public chatbot deployment;
- full unfiltered test suite.

Do not describe an omitted lane as passing.
