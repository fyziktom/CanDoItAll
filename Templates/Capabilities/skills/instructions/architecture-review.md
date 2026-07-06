Use this skill when reviewing the architecture of a C# repository.

1. Start from repository evidence, not memory or broad assumptions.
2. If CodeAnalytics MCP is available, build the narrowest useful snapshot, check dashboard health, then inspect solution/project inventory, dependencies, findings, and exact symbols before reading broad source files.
3. Record the CodeAnalytics snapshot id when the review cites MCP evidence.
4. If CodeAnalytics is unavailable, say so and use exact file reads plus `rg` as fallback evidence.
5. For large classes, partial-class clusters, project-reference changes, providers, tools, plugins, memory protocols, process drivers, runtime composition, factories, builders, catalogs, or testability work, load the C# architecture governor and apply the architecture gate before recommending implementation.
6. At least three reviewed evidence points must be concrete source, project, dependency, symbol, test, or MCP findings. `.sln`, `.slnx`, and `.csproj` files alone are not enough for behavior claims.
7. Prefer findings about responsibility concentration, fake modularity, wrong dependency direction, missing test seams, service-location shortcuts, provider/tool coupling, or construction logic in the wrong layer.
8. Do not recommend framework upgrades, more comments, more tests, or dependency-injection changes unless the evidence shows a concrete problem that justifies that recommendation.
9. Do not claim missing abstractions when the code already injects the relevant interface. Describe the actual remaining coupling or responsibility concentration.
10. Do not claim missing logging, error handling, or async support when the reviewed method already shows those constructs. Only call out deeper design problems that the evidence demonstrates.
11. Before finalizing, drop any bullet that could apply to many unrelated repositories without changing the file names, type names, or method names.
12. Return 2 to 4 findings. Fewer grounded findings are better than a longer generic list.
13. For every finding, cite exact files, projects, symbols, dependency edges, or CodeAnalytics findings, and explain the concrete behavior observed.
14. If you do not have enough evidence yet, continue reading instead of presenting a generic architecture complaint as fact.
