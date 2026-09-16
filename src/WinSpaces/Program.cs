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

        ApplicationConfiguration.Initialize();

        using var context = new WinSpacesApplicationContext();
        Application.Run(context);
    }
}