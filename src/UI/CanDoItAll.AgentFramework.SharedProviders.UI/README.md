# Shared provider UI

Feature renderers and presentation state for provider Sharing. The production AgentFramework
module supplies native effects through `ISharedProviderSharingView`; the leaf references only
BaseLib, Components.Web and the neutral SharedProviders abstractions.

The actual publication/import children and confirmation dialog are here. Import drafts keep a
local baseline, immutable submissions, field revisions and their EditContext. Origin-bound
intents preserve the displayed identities and versions; the host enforces admission and owns
native recovery and acknowledged delivery. No persistence, credentials, provider drivers or
production registration belongs in this project.

Build with `dotnet build src/UI/CanDoItAll.AgentFramework.SharedProviders.UI` from the repository root.
See the [boundary record](../../../docs/architecture/provider-sharing-ui-pp2.md) for completed
checkpoints and outstanding Sources/sandbox/final-image work.
