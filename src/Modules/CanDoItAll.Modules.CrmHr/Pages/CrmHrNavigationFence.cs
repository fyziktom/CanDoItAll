using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents.Navigation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The agent-chat navigation identity of the state a page shows: the current location with every key the page owns
// replaced by its canonical value, so a location that is not canonical yet or still in flight identifies that state.
internal static class CrmHrNavigationFence {
    public static AgentChatNavigationIdentity Create<TState>(
        NavigationManager navigation,
        UrlStateBinding<TState> url,
        params KeyValuePair<string, string?>[] additional) {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(additional);
        var overrides = url.Codec.OwnedValues(url.State).Concat(additional).ToArray();
        return AgentChatNavigationIdentity.CreateForLocation(navigation.BaseUri, navigation.Uri, overrides);
    }
}
