using WinSpaces.Modules.Widgets.Core;

namespace WinSpaces.Modules.Widgets.BuiltIn;

/// <summary>
/// Widget météo affichant les conditions actuelles (placeholder pour API météo).
/// </summary>
public sealed class WeatherWidget : WidgetBase
{
    public override string Id => "weather-" + Guid.NewGuid().ToString("N")[..8];
    public override string Name => "Météo";
    public override string Description => "Conditions météo actuelles";
    public override double Width => 200;
    public override double Height => 140;

    public string Location { get; set; } = "Paris, France";
    public int Temperature { get; private set; } = 20;
    public string Condition { get; private set; } = "Ensoleillé";
    public string Icon { get; private set; } = "☀️";
    public int Humidity { get; private set; } = 65;
    public int WindSpeed { get; private set; } = 10;

    public WeatherWidget() : base(TimeSpan.FromMinutes(15))
    {
    }

    public override async Task RefreshAsync()
    {
        // Placeholder - En production, intégrer OpenWeather API ou similaire
        await Task.Run(() =>
        {
            // Simulation données météo
            var random = new Random();
            Temperature = random.Next(15, 30);
            Humidity = random.Next(40, 80);
            WindSpeed = random.Next(5, 25);

            var conditions = new[]
            {
                ("Ensoleillé", "☀️"),
                ("Nuageux", "☁️"),
                ("Pluvieux", "🌧️"),
                ("Orageux", "⛈️")
            };

            var (cond, icon) = conditions[random.Next(conditions.Length)];
            Condition = cond;
            Icon = icon;

            Configuration["location"] = Location;
            Configuration["temperature"] = Temperature;
            Configuration["condition"] = Condition;
            Configuration["humidity"] = Humidity;
        });
    }
}
