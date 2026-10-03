using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Application = System.Windows.Application;

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
            // Asynchrone : un Invoke synchrone depuis un thread de fond bloquait ce
            // thread tant que l'UI était occupée (deadlock si l'UI l'attendait).
            Application.Current.Dispatcher.BeginInvoke(() =>
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
