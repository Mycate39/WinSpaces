using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinSpaces.Modules.Widgets.Core;
using WinSpaces.Modules.Widgets.ViewModels;

namespace WinSpaces.Modules.Widgets.Views;

/// <summary>
/// Fenêtre conteneur pour afficher un widget sur le bureau.
/// Supporte le drag & drop pour repositionnement libre.
/// </summary>
public partial class WidgetWindow : Window
{
    private readonly IWidget _widget;
    private readonly WidgetViewModelBase? _viewModel;

    public WidgetWindow(IWidget widget, WidgetViewModelBase? viewModel = null)
    {
        InitializeComponent();

        _widget = widget;
        _viewModel = viewModel;

        // Configuration fenêtre
        Width = widget.Width;
        Height = widget.Height;
        Left = widget.X;
        Top = widget.Y;
        Opacity = widget.Opacity;
        Topmost = widget.IsPinned;

        if (_viewModel != null)
        {
            DataContext = _viewModel;
        }

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadWidgetContent();
        AppLog.Info($"WidgetWindow : '{_widget.Name}' affiché à ({_widget.X}, {_widget.Y})");
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _widget.X = Left;
        _widget.Y = Top;
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            _widget.X = Left;
            _widget.Y = Top;
        }
    }

    private void LoadWidgetContent()
    {
        UserControl? content = _widget switch
        {
            Modules.Widgets.BuiltIn.SystemMonitorWidget => CreateSystemMonitorContent(),
            Modules.Widgets.BuiltIn.ClockWidget => CreateClockContent(),
            Modules.Widgets.BuiltIn.CalendarWidget => CreateCalendarContent(),
            Modules.Widgets.BuiltIn.WeatherWidget => CreateWeatherContent(),
            _ => CreateDefaultContent()
        };

        if (content != null)
        {
            WidgetContentPresenter.Content = content;
        }
    }

    private UserControl CreateSystemMonitorContent()
    {
        var widget = (Modules.Widgets.BuiltIn.SystemMonitorWidget)_widget;
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new TextBlock { Text = "System Monitor", Style = (Style)FindResource("WidgetTitleStyle") };
        Grid.SetRow(title, 0);
        grid.Children.Add(title);

        var content = new StackPanel();
        Grid.SetRow(content, 1);

        var cpuText = new TextBlock { Style = (Style)FindResource("WidgetContentStyle") };
        cpuText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CpuUsage")
        {
            StringFormat = "CPU: {0:F1}%",
            Source = widget
        });

        var ramText = new TextBlock { Style = (Style)FindResource("WidgetContentStyle"), Margin = new Thickness(0, 5, 0, 0) };
        ramText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("RamUsagePercent")
        {
            StringFormat = "RAM: {0:F1}%",
            Source = widget
        });

        content.Children.Add(cpuText);
        content.Children.Add(ramText);
        grid.Children.Add(content);

        return new UserControl { Content = grid };
    }

    private UserControl CreateClockContent()
    {
        var widget = (Modules.Widgets.BuiltIn.ClockWidget)_widget;
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        var timeText = new TextBlock
        {
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.DarkSlateGray,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        timeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TimeString") { Source = widget });

        var dateText = new TextBlock
        {
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
        dateText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DateString") { Source = widget });

        stack.Children.Add(timeText);
        stack.Children.Add(dateText);

        return new UserControl { Content = stack };
    }

    private UserControl CreateCalendarContent()
    {
        var widget = (Modules.Widgets.BuiltIn.CalendarWidget)_widget;
        var stack = new StackPanel();

        var title = new TextBlock { Style = (Style)FindResource("WidgetTitleStyle"), HorizontalAlignment = HorizontalAlignment.Center };
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("MonthYear") { Source = widget });

        var content = new TextBlock
        {
            Text = $"Aujourd'hui : {widget.CurrentDay}",
            Style = (Style)FindResource("WidgetContentStyle"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        stack.Children.Add(title);
        stack.Children.Add(content);
        return new UserControl { Content = stack };
    }

    private UserControl CreateWeatherContent()
    {
        var widget = (Modules.Widgets.BuiltIn.WeatherWidget)_widget;
        var stack = new StackPanel();

        var title = new TextBlock { Text = widget.Location, Style = (Style)FindResource("WidgetTitleStyle"), HorizontalAlignment = HorizontalAlignment.Center };
        var icon = new TextBlock { FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
        icon.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Icon") { Source = widget });

        var temp = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        temp.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Temperature") { StringFormat = "{0}°C", Source = widget });

        var condition = new TextBlock { Style = (Style)FindResource("WidgetContentStyle"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
        condition.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Condition") { Source = widget });

        stack.Children.Add(title);
        stack.Children.Add(icon);
        stack.Children.Add(temp);
        stack.Children.Add(condition);
        return new UserControl { Content = stack };
    }

    private UserControl CreateDefaultContent()
    {
        var text = new TextBlock { Text = _widget.Name, Style = (Style)FindResource("WidgetTitleStyle") };
        return new UserControl { Content = text };
    }
}

