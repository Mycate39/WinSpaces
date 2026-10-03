namespace WinSpaces;

using System.Threading;
using WinSpaces.Desktops;
using WinSpaces.Native;
using WinSpaces.Services;

/// <summary>
/// Point d'entrée WinSpaces : mutex single-instance, init des services et
/// lancement de la boucle de messages WinForms (Application.Run).
/// </summary>
internal static class Program
{
    private const string MutexName = @"Local\WinSpaces.SingleInstance.v1";

    [STAThread]
    private static void Main(string[] args)
    {
        // Mode sans échec : ni gestes, ni hooks plein écran, ni barre de menu.
        // Sert à isoler un module qui empêcherait le démarrage.
        bool safeMode = args.Any(a => a.Equals("--safe", StringComparison.OrdinalIgnoreCase));
        bool autostart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

        // Première trace : si elle manque dans le journal, le processus n'a même
        // pas atteint Main (blocage Windows, antivirus, extraction single-file…).
        AppLog.Info($"===== Lancement WinSpaces v{Application.ProductVersion.Split('+')[0]} — " +
                    $"Windows build {Native.NativeMethods.GetWindowsBuildNumber()}, pid {Environment.ProcessId}, " +
                    $"args « {string.Join(' ', args)} »{(safeMode ? " — MODE SANS ÉCHEC" : "")} =====");

        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            AppLog.Warning("Une autre instance de WinSpaces est déjà en cours d'exécution : arrêt.");
            MessageBox.Show(
                "WinSpaces est déjà en cours d'exécution.",
                "WinSpaces",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Filets de sécurité globaux : journaliser au lieu de laisser le processus
        // mourir (ou afficher la boîte de dialogue WinForms) sans trace.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Error(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception ex) return;
            AppLog.Error(new InvalidOperationException(
                $"Exception non gérée (fatale : {e.IsTerminating})", ex));
            // Le processus va mourir : on le dit au lieu de disparaître en silence.
            if (e.IsTerminating) ReportFatalError(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error(e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();

        // Une exception pendant l'initialisation (XAML, COM, hooks…) faisait
        // disparaître le processus sans aucun message : on l'affiche désormais,
        // avec le chemin du journal pour le diagnostic.
        WinSpacesApplicationContext context;
        try
        {
            context = new WinSpacesApplicationContext(safeMode, autostart);
        }
        catch (Exception ex)
        {
            ReportFatalError(ex);
            return;
        }

        using (context)
        {
            Application.Run(context);
        }
        AppLog.Info("WinSpaces arrêté normalement.");
    }

    private static void ReportFatalError(Exception ex)
    {
        AppLog.Error(new InvalidOperationException("Échec du démarrage de WinSpaces", ex));

        var root = ex;
        while (root.InnerException is not null) root = root.InnerException;

        MessageBox.Show(
            "WinSpaces a rencontré une erreur fatale.\n\n" +
            $"{root.GetType().Name} : {root.Message}\n\n" +
            $"Détails complets dans le journal :\n{AppLog.FilePath}",
            "WinSpaces — Erreur fatale",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}