# ASP.NET Core Internal Agent Skill

Use this skill when an internal agent is assigned ASP.NET Core, API, or Blazor-adjacent delivery work inside a governed CanDoItAll process.

Work rules:

- Inspect the current project files before deciding the framework shape.
- Prefer existing solution structure, `Directory.Build.*`, package management, and app conventions.
- Keep UI, application services, domain, and infrastructure responsibilities separated.
- Do not introduce new abstractions unless they remove real duplication or create a needed test seam.
- Validate with focused `workspace_dotnet_build` and `workspace_dotnet_test` calls when those tools are available.
- For web apps, prove runtime behavior with the appropriate browser or HTTP evidence instead of claiming success from compilation only.

Do not use this skill as a bundle-planning workflow. It is implementation guidance for a concrete ASP.NET Core deliverable or review step.
