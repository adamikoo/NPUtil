using System.Collections.Concurrent;

namespace NPUtil.Core.Modules;

public interface IModuleRegistry
{
    IReadOnlyList<IModule> GetRegisteredModules();
    IModule? GetModule(string id);
    void Register(IModule module);
    event EventHandler<IModule>? ModuleRegistered;
}

public class ModuleRegistry : IModuleRegistry
{
    private readonly ConcurrentDictionary<string, IModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<IModule>? ModuleRegistered;

    public IReadOnlyList<IModule> GetRegisteredModules() =>
        _modules.Values.OrderBy(m => m.SortOrder).ThenBy(m => m.Name).ToList();

    public IModule? GetModule(string id) =>
        _modules.TryGetValue(id, out var module) ? module : null;

    public void Register(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _modules[module.Id] = module;
        ModuleRegistered?.Invoke(this, module);
    }
}
