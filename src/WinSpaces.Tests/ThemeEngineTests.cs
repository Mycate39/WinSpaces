using Xunit;
using WinSpaces.Modules.Theming.Services;
using System.Windows;

namespace WinSpaces.Tests.Theming;

public class ThemeEngineTests
{
    [Fact]
    public void ApplyTheme_ShouldNotThrowException_WhenThemeDoesNotExist()
    {
        // Act & Assert
        // Note: As ThemeEngine interacts with Application.Current.Resources, 
        // this test might require a WPF context (STA Thread).
        // Since we are in a headless environment, we focus on logic.
        var engine = new ThemeEngine();
        
        var exception = Record.Exception(() => engine.ApplyTheme("NonExistentTheme"));
        
        // Assert: Log should have handled the error without crashing
        Assert.Null(exception);
    }
}
