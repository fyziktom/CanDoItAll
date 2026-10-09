using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessExecutableDefinitionResolver(IProcessAuthoringStore store, IProcessAuthoringContext context,
    ProcessAuthoringWorkspace workspace, ProcessTemplatePackLoader templates, IProcessLaunchDriverCatalogProvider drivers) {
    private const int MaximumDefinitions = 256;

    public async Task<bool> MatchesCurrentScopeAsync(Guid? projectId, ProcessProjectAdmission? admission,
        ProcessExecutableDefinitionClosure? captured, CancellationToken cancellationToken) {
        var scope = projectId is { } project ? ProcessWorkspaceShellScope.ForProject(project) : ProcessWorkspaceShellScope.Global;
        var address = await context.CaptureReadAddressAsync(scope, new("existing-launch"), cancellationToken);
        if (captured is not null && (captured.DatabaseProfileId != address.DatabaseProfileId ||
                captured.ProjectId != address.ProjectId || captured.ProjectLifetimeId != address.ProjectLifetimeId)) {
            return false;
        }
        if (address.ProjectId == Guid.Empty) {
            return admission is null;
        }
        return admission is null
            ? captured is not null
            : admission.DatabaseProfileId == address.DatabaseProfileId && admission.ProjectId == address.ProjectId &&
                admission.LifetimeId == address.ProjectLifetimeId;
    }

    public async Task<ProcessExecutableDefinitionClosure> ResolveAsync(ProcessWorkspaceShellScope scope, string key,
        ProcessProjectAdmission? admission = null, CancellationToken cancellationToken = default) {
        var address = await context.CaptureReadAddressAsync(scope, new(key), cancellationToken);
        if (admission is not null && (admission.DatabaseProfileId != address.DatabaseProfileId ||
                admission.ProjectId != address.ProjectId || admission.LifetimeId != address.ProjectLifetimeId)) {
            throw new InvalidOperationException("Executable definition resolution belongs to another project lifetime.");
        }
        var root = await SelectAsync(address, cancellationToken);
        var closure = await CloseAsync(address, root, cancellationToken);
        await RequireCurrentAsync(closure, cancellationToken);
        return closure;
    }

    public async Task<string> ResolveKeyAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionId id, CancellationToken cancellationToken) {
        var rows = await workspace.CatalogAsync(scope, cancellationToken);
        var keys = rows.Select(row => row.Address.DefinitionKey).Concat(templates.Load().Definitions.Select(definition => definition.Key)).Distinct(StringComparer.Ordinal);
        return keys.SingleOrDefault(key => ProcessTemplateKernelBuilder.CreateDefinitionId(key) == id)
            ?? throw new InvalidOperationException("The explicitly requested process definition is unavailable.");
    }

    public async Task<ProcessAuthoringContent> PinPublicationAsync(ProcessAuthoringSession baseline, ProcessAuthoringContent content, CancellationToken cancellationToken) {
        content = content with { Dependencies = new Dictionary<string, ProcessExecutableDefinitionSource>() };
        var closure = await CloseAsync(baseline.Address, Source(baseline.Address, baseline.Revision + 1, null, null, content), cancellationToken);
        ProcessAuthoringPublicationValidator.Validate(closure);
        var catalog = await drivers.LoadAsync(cancellationToken);
        foreach (var key in closure.Definitions.Keys) {
            var definition = Decode(closure, key).Definition;
            var kernel = ProcessTemplateKernelBuilder.Build(definition, content.Base.Version, catalog.StepExecutionStrategyId);
            var compiled = new ProcessInstancePlanCompiler().Compile(ProcessDefinitionCompilation.CreateRequest(kernel, definition, content.Base.Version, catalog));
            if (!compiled.Succeeded) {
                throw new InvalidOperationException("Executable definition validation failed: " + string.Join("; ", compiled.Diagnostics.Select(item => item.Message)));
            }
        }
        return content with { Dependencies = closure.Definitions.Where(pair => pair.Key != baseline.Address.DefinitionKey)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) };
    }

    public async Task RequireCurrentAsync(ProcessExecutableDefinitionClosure closure, CancellationToken cancellationToken) {
        if (closure.DatabaseProfileId != context.DatabaseProfileId) {
            throw new InvalidOperationException("The executable definition was captured in another database profile.");
        }
        Decode(closure, closure.DefinitionKey);
        await store.RequireLaunchableAsync(closure, false, cancellationToken);
    }

    public static ProcessAuthoringContent Decode(ProcessExecutableDefinitionClosure closure, string key) {
        if (!closure.Definitions.TryGetValue(key, out var source) || source.DefinitionKey != key ||
                source.DatabaseProfileId != closure.DatabaseProfileId || ProcessAuthoringCodec.Hash(source.ContentJson) != source.ContentHash) {
            throw new InvalidOperationException("The pinned executable definition is missing or failed its content identity check.");
        }
        var content = ProcessAuthoringCodec.Read(source.ContentJson);
        if (content.Definition.Key != key) {
            throw new InvalidOperationException("The pinned executable definition has a different identity.");
        }
        return content;
    }

    public static string? ExecutableIdentity(ProcessExecutableDefinitionClosure closure) {
        if (closure.Definitions.Values.All(source => source.PublicationId is null)) {
            return null;
        }
        return ProcessAuthoringCodec.Hash(string.Join('\n', closure.Definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value.ContentHash}:{pair.Value.PublicationHash}")));
    }

    public static ProcessExecutableDefinitionClosure SelectCaptured(ProcessExecutableDefinitionClosure closure, string key) {
        var selected = new Dictionary<string, ProcessExecutableDefinitionSource>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(key);
        while (queue.TryDequeue(out var next)) {
            if (selected.ContainsKey(next)) {
                continue;
            }
            var content = Decode(closure, next);
            selected.Add(next, closure.Definitions[next]);
            foreach (var child in content.Definition.Steps.Select(step => step.SubprocessContract?.DefinitionKey ?? step.SubprocessProcessKey)
                    .Where(child => !string.IsNullOrWhiteSpace(child))) {
                queue.Enqueue(child);
            }
        }
        return closure with { DefinitionKey = key, Definitions = selected };
    }

    private async Task<ProcessExecutableDefinitionSource> SelectAsync(ProcessAuthoringAddress address, CancellationToken cancellationToken) {
        var head = await store.ReadAsync(address, cancellationToken);
        if (head is { Lifecycle: ProcessAuthoringLifecycle.Archived }) {
            throw new InvalidOperationException("The selected authored definition is archived and cannot start a new launch.");
        }
        if (head is { Lifecycle: not ProcessAuthoringLifecycle.Deleted, PublishedId: { } id }) {
            var publication = await store.ReadPublicationAsync(id, cancellationToken)
                ?? throw new InvalidOperationException("The selected immutable publication is unavailable; reconciliation is required.");
            if (publication.Address != address) {
                throw new InvalidOperationException("The publication belongs to another authoring owner.");
            }
            return Source(address, publication.Revision, id, publication.ContentHash, publication.Content);
        }
        if (address.ProjectId != Guid.Empty) {
            return await SelectAsync(address with { ProjectId = Guid.Empty, ProjectLifetimeId = Guid.Empty }, cancellationToken);
        }
        return Source(address, 0, null, null, workspace.ReadTemplate(address.DefinitionKey));
    }

    private async Task<ProcessExecutableDefinitionClosure> CloseAsync(ProcessAuthoringAddress address, ProcessExecutableDefinitionSource root, CancellationToken cancellationToken) {
        var sources = new Dictionary<string, ProcessExecutableDefinitionSource>(StringComparer.Ordinal) { [root.DefinitionKey] = Flatten(root) };
        var queue = new Queue<ProcessExecutableDefinitionSource>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var source)) {
            var content = ProcessAuthoringCodec.Read(source.ContentJson);
            foreach (var pinned in content.Dependencies.Values) {
                Add(pinned);
            }
            foreach (var child in content.Definition.Steps.Select(step => step.SubprocessContract?.DefinitionKey ?? step.SubprocessProcessKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal)) {
                if (sources.ContainsKey(child)) {
                    continue;
                }
                if (source.PublicationId is not null) {
                    throw new InvalidOperationException("The immutable publication has an unresolved executable dependency.");
                }
                Add(await SelectAsync(address with { DefinitionKey = child }, cancellationToken));
            }
        }
        return new(address.DatabaseProfileId, address.ProjectId, address.ProjectLifetimeId, root.DefinitionKey, sources);

        void Add(ProcessExecutableDefinitionSource source) {
            if (source.DatabaseProfileId != address.DatabaseProfileId || ProcessAuthoringCodec.Hash(source.ContentJson) != source.ContentHash) {
                throw new InvalidOperationException("A pinned executable dependency failed its scope or content identity check.");
            }
            var flat = Flatten(source);
            if (sources.TryGetValue(source.DefinitionKey, out var existing)) {
                if (existing.ContentHash != flat.ContentHash) {
                    throw new InvalidOperationException("The executable dependency closure contains conflicting versions of one definition.");
                }
                return;
            }
            if (sources.Count >= MaximumDefinitions) {
                throw new InvalidOperationException("The executable dependency closure exceeds its bounded definition limit.");
            }
            sources.Add(source.DefinitionKey, flat);
            queue.Enqueue(source);
        }
    }

    private static ProcessExecutableDefinitionSource Flatten(ProcessExecutableDefinitionSource source) {
        var content = ProcessAuthoringCodec.Read(source.ContentJson) with { Dependencies = new Dictionary<string, ProcessExecutableDefinitionSource>() };
        var json = ProcessAuthoringCodec.Write(content);
        return source with { ContentJson = json, ContentHash = ProcessAuthoringCodec.Hash(json) };
    }

    private static ProcessExecutableDefinitionSource Source(ProcessAuthoringAddress address, long revision, Guid? publicationId,
        string? publicationHash, ProcessAuthoringContent content) {
        var json = ProcessAuthoringCodec.Write(content);
        return new(address.DatabaseProfileId, address.ProjectId, address.ProjectLifetimeId, address.DefinitionKey, revision,
            publicationId, publicationHash, ProcessAuthoringCodec.Hash(json), json);
    }
}
