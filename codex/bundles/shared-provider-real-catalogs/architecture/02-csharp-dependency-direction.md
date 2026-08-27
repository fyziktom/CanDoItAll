# C# Dependency Direction

Keep UI -> Application/ProviderManagement -> Models/Core and connector abstractions.
Shared publication consumes catalog metadata, not Blazor. No reverse dependency or new csproj
reference. Validate git diff project references plus affected compilation. Scoped CodeAnalytics
does not resolve the entire graph; no claim of full-graph cycle verification.
