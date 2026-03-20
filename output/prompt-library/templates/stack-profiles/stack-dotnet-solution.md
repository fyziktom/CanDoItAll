---
            key: stack-dotnet-solution
            id: 079cb980-2cbe-51be-9d4a-5457810717a9
            name: Stack: .NET Solution
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: .net, build, solution, tests
            promptTypes: architecture, plan, implementation, refactor, bugfix, testing, validation, performance, security, migration
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, performance-hardening, security-hardening
            phases: discovery, planning, implementation, verification
            stackTags: .net
            templateTokens: dotnet_build_command, dotnet_test_command
            ---

            ## .NET Guidance
Treat the real solution or project graph as authoritative.

Requirements:
- use the correct solution or project entry point instead of guessing,
- preserve dependency injection, nullable, analyzers, and test conventions already used in the repo,
- keep domain logic out of thin UI or host layers,
- if you add a project or contract, wire references, configuration, and tests in the same session.

Primary commands:
- build: {{dotnet_build_command}}
- test: {{dotnet_test_command}}
