# Dependencies

Panel -> session -> IProviderProfilesReads. Adapter -> existing ProviderManagement runtime/admin contracts -> their existing infrastructure. Session may use Blazor EditContext because it is a UI session, but never a Razor component.

No project/package/reference additions. Existing module still has its broad reference graph; scoped CodeAnalytics does not prove a lightweight assembly. Baseline has two known cycles (module/hosting and image-generation nested builder); no new project edges are permitted.

Composition: one TryAddScoped read adapter in AddAgentFrameworkUi; mutable session created per panel and disposed by panel. No singleton state, no service locator. Unit tests instantiate session without panel; production-composed component tests exercise registration/read adapter.
