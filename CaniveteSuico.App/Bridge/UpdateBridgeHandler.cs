using System.Text.Json;
using CaniveteSuico.App.Services;

namespace CaniveteSuico.App.Bridge;

/// <summary>
/// Handles action "INSTALL_UPDATE" sent from the frontend update banner.
/// Delegates to AppUpdater which owns the Velopack UpdateManager instance.
/// </summary>
public sealed class UpdateBridgeHandler : IBridgeHandler
{
    public string Action => "INSTALL_UPDATE";

    private readonly AppUpdater _updater;
    public UpdateBridgeHandler(AppUpdater updater) => _updater = updater;

    public Task HandleAsync(JsonElement data, Action<object> reply) =>
        _updater.InstallAsync();
}
