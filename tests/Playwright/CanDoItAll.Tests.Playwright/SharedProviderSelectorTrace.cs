using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal sealed class SharedProviderSelectorTrace(IPage page, string path) : IAsyncDisposable {
    private bool saved;

    public static async Task<SharedProviderSelectorTrace> StartAsync(IPage page, string directory, string label) {
        await page.EvaluateAsync("""
            () => {
                const entries = [];
                let previous;
                const capture = (event) => {
                    const dialog = document.querySelector('[data-testid="llm-chat-definition-editor-dialog"]');
                    const provider = dialog?.querySelector('[data-testid="llm-chat-definition-provider"]');
                    const model = dialog?.querySelector('[data-testid="llm-chat-definition-model"]');
                    const state = {
                        dialog: !!dialog,
                        dialogIdentity: dialog?.getAttributeNames().find(name => name.startsWith('_bl_')),
                        providerDisabled: provider?.disabled,
                        providerKey: provider?.value,
                        providerLabel: provider?.selectedOptions[0]?.textContent,
                        providerOptions: provider ? Array.from(provider.options, o => ({ key: o.value, label: o.text })) : [],
                        modelKey: model?.value,
                        modelOptions: model ? Array.from(model.options, o => ({ key: o.value, label: o.text })) : []
                    };
                    const fingerprint = JSON.stringify(state);
                    if (fingerprint !== previous || event !== 'render') {
                        entries.push({ sequence: entries.length, at: performance.now(), event, ...state });
                        if (entries.length > 128) entries.shift();
                        previous = fingerprint;
                    }
                };
                const onChange = event => {
                    if (['llm-chat-definition-provider', 'llm-chat-definition-model'].includes(event.target.dataset.testid)) {
                        capture('change-dispatched:' + event.target.dataset.testid);
                    }
                };
                const observer = new MutationObserver(() => capture('render'));
                observer.observe(document.body, { childList: true, subtree: true, attributes: true });
                document.addEventListener('change', onChange, true);
                capture('started');
                window.sharedProviderSelectorTrace = {
                    finish: () => {
                        capture('finished');
                        observer.disconnect();
                        document.removeEventListener('change', onChange, true);
                        return JSON.stringify(entries, null, 2);
                    }
                };
            }
            """);
        return new(page, Path.Combine(directory, $"metadata-simple-chat-{label}-selector-timeline.json"));
    }

    public async ValueTask DisposeAsync() {
        if (saved) {
            return;
        }
        var timeline = await page.EvaluateAsync<string>("() => window.sharedProviderSelectorTrace.finish()");
        await File.WriteAllTextAsync(path, timeline);
        saved = true;
    }
}
