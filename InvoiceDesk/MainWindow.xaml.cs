using System.Threading.Tasks;
using System.Windows;
using InvoiceDesk.ViewModels;
using InvoiceDesk.Views;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceDesk;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _services;

    public MainWindow(MainViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _services = services;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OpenCompanies(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<CompanyManagementWindow>();
        window.Owner = this;
        window.ShowDialog();
        await _viewModel.ReloadCompaniesAsync();
    }

    private async void OpenCustomers(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<CustomerManagementWindow>();
        window.Owner = this;
        window.ShowDialog();
        await _viewModel.ReloadCustomersAsync();
    }
}