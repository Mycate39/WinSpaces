using System.Diagnostics;
using WinSpaces.Diagnostics;
using WinSpaces.Services;

namespace WinSpaces.Modules.Spotlight.Services;

/// <summary>
/// Exécute des actions système et lance des applications/fichiers.
/// </summary>
public sealed class ActionExecutor : HealthCheckableBase
{
    public override string ComponentName => "Action Executor";
    public override bool IsHealthy => true;
    public override string StatusMessage => "Exécuteur d'actions actif";

    public ActionExecutor()
    {
        SetMetric("executions_count", 0);
    }

    /// <summary>
    /// Lance une application ou ouvre un fichier.
    /// </summary>
    public bool Execute(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };

            Process.Start(psi);

            var count = (int)(Metrics.GetValueOrDefault("executions_count", 0));
            SetMetric("executions_count", count + 1);
            SetMetric("last_executed", path);

            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException($"Erreur exécution : {path}", ex));
            return false;
        }
    }

    /// <summary>
    /// Exécute une commande système prédéfinie.
    /// </summary>
    public bool ExecuteSystemAction(SystemAction action)
    {
        try
        {
            switch (action)
            {
                case SystemAction.Lock:
                    Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                    break;

                case SystemAction.Shutdown:
                    Process.Start("shutdown", "/s /t 0");
                    break;

                case SystemAction.Restart:
                    Process.Start("shutdown", "/r /t 0");
                    break;

                case SystemAction.Sleep:
                    Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    break;

                case SystemAction.SignOut:
                    Process.Start("shutdown", "/l");
                    break;

                default:
                    return false;
            }

            var count = (int)(Metrics.GetValueOrDefault("system_actions_count", 0));
            SetMetric("system_actions_count", count + 1);
            SetMetric("last_system_action", action.ToString());

            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error(new InvalidOperationException($"Erreur action système : {action}", ex));
            return false;
        }
    }
}

public enum SystemAction
{
    Lock,
    Shutdown,
    Restart,
    Sleep,
    SignOut
}
