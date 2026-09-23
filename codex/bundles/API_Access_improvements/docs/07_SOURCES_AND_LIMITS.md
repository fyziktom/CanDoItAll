# 7. Source index, provenance and review limits

## 7.1 Provenance

Prepared on 20 September 2026. Repository evidence was read through the connected GitHub capability, using explicit commit refs rather than assuming code-search results represented development. File searches use the default branch and were used for path discovery only where followed by a pinned read.

The comparison reported UI ahead 35 / behind 223 relative to development. The common-base comparison identifies colleague additions; direct head-to-head diffs also contain development hardening that must not be reversed.

- [Development snapshot](https://github.com/fyziktom/CanDoItAll/tree/b82ffc57283f5e4819d82322e1c5bf836dcd9536)
- [UI snapshot](https://github.com/fyziktom/CanDoItAll/tree/e101d5db1478ea329a572db79c0104b927d97f15)
- [Reviewed common-base comparison](https://github.com/fyziktom/CanDoItAll/compare/b82ffc57283f5e4819d82322e1c5bf836dcd9536...e101d5db1478ea329a572db79c0104b927d97f15)
- [UI latest commit and patches](https://github.com/fyziktom/CanDoItAll/commit/e101d5db1478ea329a572db79c0104b927d97f15)

## 7.2 Development evidence

**D01 — Options, status, machine issuer.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs). Read; configuration, issuance and claims reviewed.

**D02 — JWT registration and policies.** [src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs). Read; authentication and authorization composition reviewed.

**D03 — API/documentation mapping and token endpoint.** [src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs). Read; parent group, flags, anonymous status and issuance reviewed.

**D04 — Private file token registry.** [src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiTokenRegistry.cs). Read; durability, schema, empty-scope invariant and mutation locks reviewed.

**D05 — Token administration application service.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs). Read together with WebApiTokenAdministrationAccess.

**D05 — Token administration authority adapter.** [src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs). Read; local-operator versus bearer-scope authority reviewed.

**D06 — Scope claim matching.** [src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs). Read; exact/broad matching and case semantics reviewed.

**D07 — Managed token validator.** [src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs). Read; legacy branch, managed ID and fail-closed registry checks reviewed.

**D08 — Shipped Settings page.** [src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor). Read; current API access tab and panel integration reviewed.

**D09 — Token panel.** [src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor). Read; scope picker, list access and one-time display reviewed.

**D10 — Host pipeline.** [src/App/CanDoItAll.Web/Program.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Program.cs). Targeted lines 70-165; original peer, forwarding, auth, error handling and route composition.

**D11 — Workflow API.** [src/App/CanDoItAll.Web/Api/WorkflowsApi.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Api/WorkflowsApi.cs). Targeted lines 1-250; routing, current declarations and authentication remarks.

**D12 — Existing API authorization regressions.** [tests/Integration/CanDoItAll.Tests.Integration/ApiAccessAuthorizationIntegrationTests.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/tests/Integration/CanDoItAll.Tests.Integration/ApiAccessAuthorizationIntegrationTests.cs). Targeted lines 1-220; actual assertions read, not executed.

**D13 — Local operator trust.** [src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs). Read; explicit interactive trust and original/effective peer checks.

**D14 — Scope names.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccessScopeNames.cs). Read; exact currently defined scope strings.

**D14 — Scope catalog.** [src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs). Read; labels, parser and managed-token claim constants.

**D15 — Workspace defaults model and service.** [src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs). Read; safe defaults fields and current database/activity operations.

**D16 — Agent entry point.** [AGENTS.md](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/AGENTS.md). Read; engineering, static gate and UI seams requirements.

**D16 — Engineering rules.** [.github/copilot-instructions.md](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/.github/copilot-instructions.md). Read; owner/dependency rules, English comments, shared components, no Radzen.

**D17 — Testing policy.** [docs/testing.md](https://github.com/fyziktom/CanDoItAll/blob/b82ffc57283f5e4819d82322e1c5bf836dcd9536/docs/testing.md). Targeted lines 1-200; test workspaces, discovery counts and broad-gate triggers.

**D18 — CodeAnalytics execution guidance.** [SharedInfo skill](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/776a6a329dce5764ba07f6203a8d2ed103d39c2b/codex/skills/candoitall-codeanalytics-mcp/SKILL.md). Read the complete skill, including exact impacted-test capability, request fields and selector semantics. The MCP itself was not executed against a local CanDoItAll workspace during this preparation.

## 7.3 UI-branch evidence

**U01 — Relevant changed-file comparison and latest commit patches.** [UI commit](https://github.com/fyziktom/CanDoItAll/commit/e101d5db1478ea329a572db79c0104b927d97f15) and [comparison](https://github.com/fyziktom/CanDoItAll/compare/b82ffc57283f5e4819d82322e1c5bf836dcd9536...e101d5db1478ea329a572db79c0104b927d97f15). Inspected API/Program/ProjectStructure patches; not a review of every alternative UI component.

**U02 — Access route metadata.** [src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs). Read; no new login/account implementation in this delta.

**U03 — Process definitions.** [src/App/CanDoItAll.Web/Api/ProcessDefinitionsApi.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Api/ProcessDefinitionsApi.cs). Read; all four added read handlers.

**U04 — Workflow templates.** [src/App/CanDoItAll.Web/Api/WorkflowsApi.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Api/WorkflowsApi.cs). Targeted first 360 lines plus relevant commit patches/helpers; template creation sequence inspected.

**U05 — Workspace defaults mapping.** [src/App/CanDoItAll.Web/Program.cs](https://github.com/fyziktom/CanDoItAll/blob/e101d5db1478ea329a572db79c0104b927d97f15/src/App/CanDoItAll.Web/Program.cs). Targeted host setup and lines 880-930; unguarded direct app mappings confirmed.

## 7.4 Primary external references

External references were checked during preparation. They support framework/security mechanics; the proposed product boundaries and default values remain engineering decisions for this task, not requirements attributed wholesale to Microsoft or OWASP. No external source files or long quotations are bundled.

**S01 — [Microsoft: Configure JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).** Validation, challenge behavior, and the distinction between this bounded local design and general standards-based token acquisition.

**S02 — [Microsoft: Hash passwords in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0).** Use PasswordHasher rather than designing password storage directly with low-level primitives.

**S02 — [Microsoft: PasswordHasherOptions.IterationCount](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasheroptions.iterationcount?view=aspnetcore-10.0).** Configurable work factor; check the package implementation rather than blindly accepting defaults.

**S03 — [OWASP: Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).** Salted slow hashes, PRF-specific work factors and work-factor upgrading. Retrieved guidance gives PBKDF2-HMAC-SHA512 220,000 iterations; recheck at implementation time.

**S04 — [Microsoft: Proxy and load balancer configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).** Trusted forwarders, original request information and middleware ordering.

**S05 — [IETF RFC 8725: JWT Best Current Practices](https://www.rfc-editor.org/rfc/rfc8725.html).** Algorithm validation, audience, cross-JWT confusion, explicit token context and mutually exclusive validation rules.

**S06 — [Microsoft: CORS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0).** Exact allowed origins and browser request policy; not an authorization replacement.

**S07 — [Microsoft: Policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0).** Requirements/policies compose, so parent requirements must not unintentionally exclude precise child capabilities.

## 7.5 Limits and things Codex must still verify

This was targeted source analysis, not an exhaustive execution audit. It did not run the application, compile the solution, fetch every transitive dependency, exercise API/Playwright tests, prove every endpoint policy, or validate the deployed reverse proxy. Reading an existing test is not a passing test result.

The UI branch's eight substantive method/route additions and the observed mapping gaps are directly grounded in the inspected files and patches. Statements about complete authorization coverage are requirements for the implementation inventory, not a claim that every current endpoint was read. Runtime/download/shared-provider aliases, process projection ownership at the actual future HEAD, existing hasher packages, current browser test classes and all current test discovery counts must be resolved in the checkout.

The package deliberately does not define EGCP, assume a particular external identity protocol for it, or attempt its integration. It does not recommend importing the colleague's old UI/framework versions.

The Git helper included here is optional and read-only with respect to the checkout. It was validated separately using a synthetic temporary Git repository; that does not provide CanDoItAll application evidence. All application scenarios in the evidence template remain not_run.

## 7.6 Decisions the implementer may refine

Final internal type placement, DTO names, exact section vocabulary, the shared signing helper, concurrency response convention and the transaction/compensation mechanism for workflow drafts can follow current owners and established contracts. Record material deviations in the implementation report.

Do not silently refine away the fixed invariants: independently disabled HTTP administration, verified admin authority, preserved local SSR operation, preserved legitimate machine tokens, actual section enforcement, account-change revocation, safe secrets, and real API/UI proof.
