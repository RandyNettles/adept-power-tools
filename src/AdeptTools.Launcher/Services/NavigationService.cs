namespace AdeptTools.Launcher.Services;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(Type viewModelType);
    event Action<object>? CurrentPageChanged;
    object? CurrentPage { get; }
}

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public object? CurrentPage { get; private set; }
    public event Action<object>? CurrentPageChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        NavigateTo(typeof(TViewModel));
    }

    public void NavigateTo(Type viewModelType)
    {
        var viewModel = _serviceProvider.GetService(viewModelType)
            ?? throw new InvalidOperationException($"ViewModel {viewModelType.Name} is not registered.");

        CurrentPage = viewModel;
        CurrentPageChanged?.Invoke(viewModel);
    }
}
