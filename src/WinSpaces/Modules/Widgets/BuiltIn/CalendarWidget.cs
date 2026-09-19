using WinSpaces.Modules.Widgets.Core;

namespace WinSpaces.Modules.Widgets.BuiltIn;

/// <summary>
/// Widget calendrier affichant le mois en cours avec mise en valeur de la date du jour.
/// </summary>
public sealed class CalendarWidget : WidgetBase
{
    public override string Id => "calendar-" + Guid.NewGuid().ToString("N")[..8];
    public override string Name => "Calendrier";
    public override string Description => "Mini-calendrier du mois en cours";
    public override double Width => 220;
    public override double Height => 200;

    public DateTime CurrentDate { get; private set; } = DateTime.Now;
    public string MonthYear => CurrentDate.ToString("MMMM yyyy");
    public int CurrentDay => CurrentDate.Day;
    public List<int> DaysInMonth { get; private set; } = new();

    public CalendarWidget() : base(TimeSpan.FromHours(1))
    {
    }

    public override Task RefreshAsync()
    {
        CurrentDate = DateTime.Now;
        
        var daysCount = DateTime.DaysInMonth(CurrentDate.Year, CurrentDate.Month);
        DaysInMonth = Enumerable.Range(1, daysCount).ToList();

        Configuration["current_date"] = CurrentDate;
        Configuration["month_year"] = MonthYear;
        Configuration["current_day"] = CurrentDay;
        
        return Task.CompletedTask;
    }
}
