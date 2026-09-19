using WinSpaces.Modules.Widgets.Core;

namespace WinSpaces.Modules.Widgets.BuiltIn;

/// <summary>
/// Widget horloge affichant l'heure courante (analogique ou numérique).
/// </summary>
public sealed class ClockWidget : WidgetBase
{
    public override string Id => "clock-" + Guid.NewGuid().ToString("N")[..8];
    public override string Name => "Horloge";
    public override string Description => "Horloge numérique élégante";
    public override double Width => 180;
    public override double Height => 80;

    public DateTime CurrentTime { get; private set; } = DateTime.Now;
    public string TimeString => CurrentTime.ToString("HH:mm:ss");
    public string DateString => CurrentTime.ToString("dddd d MMMM yyyy");

    public ClockWidget() : base(TimeSpan.FromSeconds(1))
    {
    }

    public override Task RefreshAsync()
    {
        CurrentTime = DateTime.Now;
        Configuration["current_time"] = CurrentTime;
        Configuration["time_string"] = TimeString;
        return Task.CompletedTask;
    }
}
