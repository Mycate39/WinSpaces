namespace WinSpaces.Diagnostics;

/// <summary>
/// Définit un composant pouvant être interrogé sur son état de santé.
/// </summary>
public interface IHealthCheckable
{
    string ComponentName { get; }
    bool IsHealthy { get; }
    string StatusMessage { get; }
}
