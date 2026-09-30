# Configuration UI sandbox

Runs the actual neutral schema renderer and BaseLib without Workspace, Security or a provider.
It exercises raw fields, validation, unavailable secret references and explicit acquisition.
The trusted renderer registry remains in the production host and is never loaded here.

Run `dotnet run --project src/Sandboxes/CanDoItAll.Configuration.UiSandbox` from the repository
root. Source and published Production browser coverage is `ConfigurationSandboxBrowserTests`.
The linked application theme is a static asset; build it with `npm run tailwind:build` if absent.
