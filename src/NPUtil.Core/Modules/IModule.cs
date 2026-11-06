namespace NPUtil.Core.Modules;

/// <summary>
/// Defines metadata and entry points for any modular utility registered in NPUtil.
/// </summary>
public interface IModule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string IconGlyph { get; }
    int SortOrder { get; }
    bool IsEnabled { get; set; }

    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
    object CreateView();
}
