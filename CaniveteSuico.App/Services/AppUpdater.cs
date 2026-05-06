using CaniveteSuico.App.Logging;
using Velopack;
using Velopack.Sources;

namespace CaniveteSuico.App.Services;

/// <summary>
/// Manages the full Velopack update lifecycle:
///   1. CheckAsync     — silently checks for a newer release
///   2. INSTALL_UPDATE — downloads deltas + restarts into the new version
///
/// Update source: GitHub Releases.
/// Set GITHUB_REPO_URL to your public GitHub repo URL.
/// </summary>
public sealed class AppUpdater
{
    // ── CONFIGURE THIS ───────────────────────────────────────────────────
    // Replace with your GitHub repo URL before distributing.
    // Example: "https://github.com/marcos/CaniveteSuico"
    private const string GitHubRepoUrl = "https://github.com/Marc0zDev/smartkit-app";
    // ─────────────────────────────────────────────────────────────────────

    private readonly Action<object> _reply;
    private UpdateManager? _manager;
    private UpdateInfo? _pendingUpdate;

    public AppUpdater(Action<object> reply) => _reply = reply;

    /// <summary>
    /// Called from MainWindow after the WebView is ready.
    /// Runs in background — never throws to the caller.
    /// </summary>
    public async Task CheckAsync()
    {
        try
        {
            AppLogger.Info("[Updater] Verificando atualizações…");

            _manager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));
            _pendingUpdate = await _manager.CheckForUpdatesAsync();

            if (_pendingUpdate is null)
            {
                AppLogger.Info("[Updater] Nenhuma atualização disponível.");
                return;
            }

            string newVer = _pendingUpdate.TargetFullRelease.Version.ToString();
            AppLogger.Info($"[Updater] Nova versão disponível: {newVer}");

            _reply(new { type = "UPDATE_AVAILABLE", version = newVer });
        }
        catch (Exception ex)
        {
            // Update check failure is non-critical — log and move on.
            AppLogger.Warn($"[Updater] Falha ao verificar atualizações: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads the pending update and restarts into the new version.
    /// Progress is streamed back via reply({ type:"UPDATE_PROGRESS", percent:N }).
    /// </summary>
    public async Task InstallAsync()
    {
        if (_manager is null || _pendingUpdate is null)
        {
            _reply(new { type = "ERROR", action = "INSTALL_UPDATE",
                         message = "Nenhuma atualização pendente encontrada." });
            return;
        }

        try
        {
            string ver = _pendingUpdate.TargetFullRelease.Version.ToString();
            AppLogger.Info($"[Updater] Baixando versão {ver}…");

            _reply(new { type = "UPDATE_PROGRESS", percent = 0,
                         message = $"Baixando versão {ver}…" });

            await _manager.DownloadUpdatesAsync(_pendingUpdate, pct =>
            {
                AppLogger.Debug($"[Updater] Download: {pct}%");
                _reply(new { type = "UPDATE_PROGRESS", percent = pct,
                             message = $"Baixando… {pct}%" });
            });

            AppLogger.Info("[Updater] Download concluído. Reiniciando…");
            _reply(new { type = "UPDATE_PROGRESS", percent = 100,
                         message = "Aplicando atualização e reiniciando…" });

            await Task.Delay(800); // allow frontend to render the final message
            _manager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "[Updater] Falha ao instalar atualização");
            _reply(new { type = "ERROR", action = "INSTALL_UPDATE", message = ex.Message });
        }
    }
}
