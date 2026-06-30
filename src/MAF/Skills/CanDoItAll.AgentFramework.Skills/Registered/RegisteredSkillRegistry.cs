using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

public sealed class RegisteredSkillRegistry : IRegisteredSkillRegistry
{
    private readonly Dictionary<ImplementationKey, RegisteredSkillBinding> bindings = [];

    public void Register(RegisteredSkillBinding binding)
    {
        bindings[binding.RegisteredSkillKey] = binding;
    }

    public bool TryResolve(ImplementationKey registeredSkillKey, out RegisteredSkillBinding binding)
        => bindings.TryGetValue(registeredSkillKey, out binding!);

    public IReadOnlyList<RegisteredSkillBinding> List()
        => bindings.Values.ToArray();
}
