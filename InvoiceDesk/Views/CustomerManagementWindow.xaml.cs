using System.ComponentModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using InvoiceDesk.Resources;
using InvoiceDesk.Services;
using InvoiceDesk.ViewModels;

namespace InvoiceDesk.Views;

public partial class CustomerManagementWindow : Window
{
    private readonly CustomerManagementViewModel _viewModel;
    private readonly ILanguageService _languageService;

    public CustomerManagementWindow(CustomerManagementViewModel viewModel, ILanguageService languageService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _languageService = languageService;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _languageService.CultureChanged += OnCultureChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        // Ensure the country dropdown sees the collection even if XAML binding fails.
        CustomersGrid.ItemsSource = _viewModel.Customers;
        // Assign ItemsSource for the country column if it exists.
        foreach (var column in CustomersGrid.Columns.OfType<System.Windows.Controls.DataGridComboBoxColumn>())
        {
            column.ItemsSource = _viewModel.Countries;
        }

        UpdateEikVisibility();
        // Force a binding refresh so visibility triggers pick up initial values.
        CustomersGrid.Items.Refresh();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        CustomersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        CustomersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        try
        {
            if (_viewModel.SaveCommand is IAsyncRelayCommand asyncCommand)
            {
                await asyncCommand.ExecuteAsync(null);
            }
            else if (_viewModel.SaveCommand.CanExecute(null))
            {
                _viewModel.SaveCommand.Execute(null);
            }

            MessageBox.Show(this, Strings.MessageCustomerSaved, Strings.Save, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, Strings.Save, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCultureChanged(object? sender, CultureInfo e)
    {
        UpdateEikVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomerManagementViewModel.IsBulgarianUi))
        {
            UpdateEikVisibility();
        }
    }

    private void UpdateEikVisibility()
    {
        CustomerEikColumn.Visibility = Visibility.Visible;
    }
}
