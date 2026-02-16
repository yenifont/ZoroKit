using CommunityToolkit.Mvvm.ComponentModel;

namespace ZaraGON.UI.Services;

public sealed partial class NavigationService : ObservableObject
{
    private readonly Dictionary<string, Func<ObservableObject>> _viewModelFactories = [];
    private readonly Dictionary<string, ObservableObject> _viewModelCache = [];

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private string _currentView = "Dashboard";

    public void RegisterView(string name, Func<ObservableObject> factory)
    {
        _viewModelFactories[name] = factory;
    }

    public void NavigateTo(string viewName)
    {
        if (_viewModelFactories.TryGetValue(viewName, out var factory))
        {
            if (!_viewModelCache.TryGetValue(viewName, out var vm))
            {
                vm = factory();
                _viewModelCache[viewName] = vm;
            }

            CurrentViewModel = vm;
            CurrentView = viewName;
        }
    }
}
