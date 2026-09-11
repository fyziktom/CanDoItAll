using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafPreparedSkillsSource(AgentSkillsSource inner, string root, MafSkillSourcePreparation preparation)
    : DelegatingAgentSkillsSource(inner) {
    public override async Task<IList<AgentSkill>> GetSkillsAsync(AgentSkillsSourceContext context,
        CancellationToken cancellationToken = default) {
        var skills = await InnerSource.GetSkillsAsync(context, cancellationToken);
        return skills.Select(skill => skill is AgentFileSkill file
            ? (AgentSkill)new MafPreparedSkill(file,
                new(MafSkillOriginKind.File, null, root, Path.GetFullPath(file.Path), string.Empty), preparation)
            : throw MafContextToolSourceContract.MissingSource()).ToList();
    }
}

internal sealed class MafPreparedSkill(AgentSkill inner, MafSkillOrigin origin, MafSkillSourcePreparation preparation) : AgentSkill {
    internal MafSkillOrigin Origin => origin;
    public override AgentSkillFrontmatter Frontmatter => inner.Frontmatter;

    internal static AgentSkill Inline(AgentSkill skill, CapabilityCatalogItem capability, MafSkillSourcePreparation preparation)
        => new MafPreparedSkill(skill, new(MafSkillOriginKind.Inline, capability.Id, string.Empty, string.Empty, string.Empty), preparation);

    internal static AgentSkill Registered(AgentSkill skill, CapabilityCatalogItem capability, MafSkillSourcePreparation preparation)
        => new MafPreparedSkill(skill, new(MafSkillOriginKind.Registered, capability.Id, string.Empty,
            skill is AgentFileSkill file ? Path.GetFullPath(file.Path) : string.Empty,
            skill.GetType().AssemblyQualifiedName ?? throw MafContextToolSourceContract.MissingSource()), preparation);

    private ValueTask RequireAsync(CancellationToken cancellationToken)
        => preparation.RequireInvocationAsync(new(Frontmatter.Name, origin), cancellationToken);

    public override async ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default) {
        await RequireAsync(cancellationToken);
        var result = await inner.GetContentAsync(cancellationToken);
        await CompleteAsync(MafSkillResultKind.Content, string.Empty, string.Empty, cancellationToken);
        return result;
    }

    public override async ValueTask<AgentSkillResource?> GetResourceAsync(string name, CancellationToken cancellationToken = default) {
        await RequireAsync(cancellationToken);
        var resource = await inner.GetResourceAsync(name, cancellationToken);
        return resource is null ? null : new PreparedResource(resource, this);
    }

    public override async ValueTask<AgentSkillScript?> GetScriptAsync(string name, CancellationToken cancellationToken = default) {
        await RequireAsync(cancellationToken);
        var script = await inner.GetScriptAsync(name, cancellationToken);
        return script is null ? null : new PreparedScript(script, this, inner);
    }

    private string ResolveResourcePath(AgentSkillResource resource)
        => origin.Kind == MafSkillOriginKind.File && inner is AgentFileSkill file
            ? Path.GetFullPath(resource.Name, file.Path)
            : string.Empty;

    private ValueTask CompleteAsync(MafSkillResultKind kind, string name, string path, CancellationToken cancellationToken)
        => preparation.ResultDisclosure is { } disclosure
            ? disclosure.RecordAsync(new(Frontmatter.Name, origin), kind, name, path, cancellationToken)
            : ValueTask.CompletedTask;

    private sealed class PreparedResource(AgentSkillResource resource, MafPreparedSkill skill)
        : AgentSkillResource(resource.Name, resource.Description) {
        public override async Task<object?> ReadAsync(IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default) {
            await skill.RequireAsync(cancellationToken);
            var result = await resource.ReadAsync(serviceProvider, cancellationToken);
            var path = skill.ResolveResourcePath(resource);
            var kind = path.Length > 0 ? MafSkillResultKind.FileResource
                : skill.Origin.Kind == MafSkillOriginKind.Inline ? MafSkillResultKind.InlineResource : MafSkillResultKind.Unsupported;
            await skill.CompleteAsync(kind, resource.Name, path, cancellationToken);
            return result;
        }
    }

    private sealed class PreparedScript(AgentSkillScript script, MafPreparedSkill skill, AgentSkill original)
        : AgentSkillScript(script.Name, script.Description) {
        public override JsonElement? ParametersSchema => script.ParametersSchema;

        public override async Task<object?> RunAsync(AgentSkill invokingSkill, JsonElement? arguments = null,
            IServiceProvider? serviceProvider = null, CancellationToken cancellationToken = default) {
            await skill.RequireAsync(cancellationToken);
            var result = await script.RunAsync(original, arguments, serviceProvider, cancellationToken);
            await skill.CompleteAsync(script is AgentFileSkillScript ? MafSkillResultKind.FileScript : MafSkillResultKind.Unsupported,
                script.Name, script is AgentFileSkillScript file ? file.FullPath : string.Empty, cancellationToken);
            return result;
        }
    }
}
