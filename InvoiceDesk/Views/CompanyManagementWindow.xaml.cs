using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using InvoiceDesk.Models;
using InvoiceDesk.Resources;
using InvoiceDesk.Services;
using InvoiceDesk.ViewModels;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;

namespace InvoiceDesk.Views;

public partial class CompanyManagementWindow : Window
{
    private readonly CompanyManagementViewModel _viewModel;
    private readonly ILanguageService _languageService;

    public CompanyManagementWindow(CompanyManagementViewModel viewModel, ILanguageService languageService)
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
        // Ensure the country dropdown has data even if XAML binding fails to resolve.
        CountryColumn.ItemsSource = _viewModel.Countries;
        UpdateEikVisibility();
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Commit any pending cell/row edits so the latest values are saved.
        CompaniesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CompaniesGrid.CommitEdit(DataGridEditingUnit.Row, true);

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

            MessageBox.Show(this, Strings.MessageCompanySaved, Strings.Save, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, Strings.Save, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnBrowseLogo(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not Company company)
        {
            return;
        }

        // End any edit/new transactions before opening the dialog so collection refresh is allowed.
        CompaniesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CompaniesGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Title = "Select logo image"
        };

        if (!string.IsNullOrWhiteSpace(company.LogoPath))
        {
            try
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(company.LogoPath);
                dialog.FileName = System.IO.Path.GetFileName(company.LogoPath);
            }
            catch
            {
                // Ignore invalid initial paths and fall back to the default dialog location.
            }
        }

        var result = dialog.ShowDialog(this);
        if (result == true)
        {
            company.LogoPath = dialog.FileName;

            // Force the grid to refresh so the new logo path shows immediately.
            var view = CollectionViewSource.GetDefaultView(CompaniesGrid.ItemsSource);
            if (view is IEditableCollectionView editable)
            {
                if (editable.IsAddingNew)
                {
                    editable.CommitNew();
                }

                if (editable.IsEditingItem)
                {
                    editable.CommitEdit();
                }
            }

            view?.Refresh();
        }
    }

    private static bool IsDigitsOnly(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithinEikLimit(System.Windows.Controls.TextBox textBox, string incoming)
    {
        // Account for replacing selected text, not just inserting at the caret.
        var currentLength = textBox.Text?.Length ?? 0;
        var proposedLength = currentLength - textBox.SelectionLength + incoming.Length;
        return proposedLength <= 13;
    }

    private void OnEikPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        e.Handled = !IsDigitsOnly(e.Text) || !IsWithinEikLimit(textBox, e.Text);
    }

    private void OnEikPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var pasted = e.DataObject.GetData(DataFormats.Text)?.ToString() ?? string.Empty;
            if (!IsDigitsOnly(pasted) || !IsWithinEikLimit(textBox, pasted))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void OnCultureChanged(object? sender, CultureInfo e)
    {
        UpdateEikVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompanyManagementViewModel.IsBulgarianUi))
        {
            UpdateEikVisibility();
        }
    }

    private void UpdateEikVisibility()
    {
        EikColumn.Visibility = Visibility.Visible;
    }

}
