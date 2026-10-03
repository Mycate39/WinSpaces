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
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
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
            if (e.ExceptionObject is Exception ex) AppLog.Error(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error(e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();

        using var context = new WinSpacesApplicationContext();
        Application.Run(context);
    }
}