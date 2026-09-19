using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WinSpaces.ViewModels;

/// <summary>
/// Classe de base pour tous les ViewModels : implémente INotifyPropertyChanged.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
        else
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
