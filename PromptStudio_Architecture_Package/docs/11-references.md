# 11 — References

This architecture package is primarily a design artifact. The following references were used to confirm important current platform and tooling assumptions relevant to the proposed architecture.

## ASP.NET Core / Blazor

1. ASP.NET Core Blazor render modes  
   https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0

2. ASP.NET Core Blazor with Entity Framework Core  
   https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0

3. ASP.NET Core Blazor server-side state management  
   https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/server?view=aspnetcore-10.0

4. ASP.NET Core Blazor forms validation  
   https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-10.0

5. ASP.NET Core file uploads  
   https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-10.0

## Developer tooling and local APIs

- `dotnet watch` command reference
  https://learn.microsoft.com/dotnet/core/tools/dotnet-watch

- ASP.NET Core file watcher guidance
  https://learn.microsoft.com/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-10.0

- ASP.NET Core OpenAPI document generation
  https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0

- ASP.NET Core OpenAPI overview
  https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0

- ASP.NET Core Minimal API responses, including SSE
  https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0

- ASP.NET Core Blazor protected browser storage
  https://learn.microsoft.com/aspnet/core/blazor/state-management/protected-browser-storage?view=aspnetcore-10.0

## EF Core and persistence

6. DbContext lifetime, configuration, and initialization  
   https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/

7. Design-time DbContext creation  
   https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation

8. EF Core In-Memory provider  
   https://learn.microsoft.com/en-us/ef/core/providers/in-memory/

9. Choosing a testing strategy for EF Core  
   https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy

10. Background tasks with hosted services in ASP.NET Core  
    https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0

11. Channels in .NET  
    https://learn.microsoft.com/en-us/dotnet/core/extensions/channels

12. Npgsql EF Core provider  
    https://www.npgsql.org/efcore/

## Security and configuration

13. ASP.NET Core Data Protection overview  
    https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0

14. Data Protection configuration overview  
    https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0

15. Options pattern in ASP.NET Core  
    https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0

16. Options validation in .NET  
    https://learn.microsoft.com/en-us/dotnet/core/extensions/options

## AI integration

17. OpenAI Responses API reference  
    https://platform.openai.com/docs/api-reference/responses

18. Official OpenAI .NET SDK repository  
    https://github.com/openai/openai-dotnet

19. Microsoft.Extensions.AI libraries  
    https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai

20. .NET AI overview  
    https://learn.microsoft.com/en-us/dotnet/ai/overview

21. AI tool calling in .NET  
    https://learn.microsoft.com/en-us/dotnet/ai/conceptual/ai-tools

22. Ollama API introduction  
    https://docs.ollama.com/api/introduction

23. Ollama authentication  
    https://docs.ollama.com/api/authentication

24. Ollama OpenAI compatibility  
    https://docs.ollama.com/api/openai-compatibility

25. Ollama cloud documentation  
    https://docs.ollama.com/cloud

## Playwright and testing automation

26. Playwright introduction  
    https://playwright.dev/docs/intro

27. Playwright test agents  
    https://playwright.dev/docs/test-agents

28. Playwright release notes  
    https://playwright.dev/docs/release-notes

29. Playwright web server configuration  
    https://playwright.dev/docs/test-webserver

30. Playwright best practices  
    https://playwright.dev/docs/best-practices

31. Playwright accessibility testing  
    https://playwright.dev/docs/accessibility-testing

## Local repository reference packs

32. Shared Blazor component library  
    `C:\repositories\CanDoItAll\src\CanDoItAll.Components`

33. Shared component guidance  
    `C:\repositories\CanDoItAll\docs\ui-shared-components\README.md`

34. Shared component roadmap  
    `C:\repositories\CanDoItAll\docs\ui-shared-components\recommendations\missing-components.md`

35. Project structure canvas source-analysis pack  
    `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\README.md`

36. Project calendar source-analysis pack  
    `C:\repositories\CanDoItAll\docs\canvas-events-calendar\README.md`

## Note

These references inform the architecture but do not constrain it to every implementation detail. The proposed solution intentionally wraps vendor/platform specifics behind the application’s own module and service abstractions where long-term flexibility matters.
