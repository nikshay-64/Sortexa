using System;

namespace Sortexa.App.Services;

public interface INavigationService
{
    event Action<object>? CurrentViewChanged;
    object? CurrentView { get; }
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(object viewModel);
}

public sealed class NavigationService : INavigationService
{
    private readonly Func<Type, object> _viewModelFactory;
    private object? _currentView;

    public NavigationService(Func<Type, object> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public event Action<object>? CurrentViewChanged;

    public object? CurrentView
    {
        get => _currentView;
        private set
        {
            if (_currentView != value && value != null)
            {
                _currentView = value;
                CurrentViewChanged?.Invoke(value);
            }
        }
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var vm = _viewModelFactory(typeof(TViewModel));
        CurrentView = vm;
    }

    public void NavigateTo(object viewModel)
    {
        CurrentView = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
