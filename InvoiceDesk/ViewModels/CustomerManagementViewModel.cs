using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceDesk.Models;
using InvoiceDesk.Services;
using InvoiceDesk.Resources;

namespace InvoiceDesk.ViewModels;

public partial class CustomerManagementViewModel : ObservableObject
{
    private readonly CustomerService _customerService;
    private readonly ICompanyContext _companyContext;
    private readonly ILanguageService _languageService;

    [ObservableProperty]
    private ObservableCollection<Customer> customers = new();

    [ObservableProperty]
    private Customer? selectedCustomer;

    public ObservableCollection<CountryOption> Countries { get; } = new();

    public CustomerManagementViewModel(CustomerService customerService, ICompanyContext companyContext, ILanguageService languageService)
    {
        _customerService = customerService;
        _companyContext = companyContext;
        _languageService = languageService;
        _languageService.CultureChanged += OnCultureChanged;
        OnPropertyChanged(nameof(IsBulgarianUi));
        LoadCountryOptions();
    }

    public bool IsBulgarianUi => string.Equals(_languageService.CurrentCulture.TwoLetterISOLanguageName, "bg", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        var list = await _customerService.GetCustomersAsync();
        Customers = new ObservableCollection<Customer>(list);
    }

    [RelayCommand]
    private void AddCustomer()
    {
        Customers.Add(new Customer
        {
            CompanyId = _companyContext.CurrentCompanyId,
            Name = "",
            CountryCode = "",
            Address = "",
            Email = "",
            Phone = ""
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        foreach (var customer in Customers)
        {
            await _customerService.SaveAsync(customer);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedCustomer == null)
        {
            return;
        }

        var target = SelectedCustomer;
        if (target.Id != 0)
        {
            try
            {
                await _customerService.DeleteAsync(target.Id);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, Strings.Delete, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        Customers.Remove(target);
    }

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        OnPropertyChanged(nameof(IsBulgarianUi));
    }

    private void LoadCountryOptions()
    {
        Countries.Clear();

        var regions = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(c => new RegionInfo(c.Name))
            .GroupBy(r => r.TwoLetterISORegionName)
            .Select(g => g.First())
            .OrderByDescending(r => string.Equals(r.TwoLetterISORegionName, "BG", StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.EnglishName);

        var any = false;
        foreach (var region in regions)
        {
            Countries.Add(new CountryOption(region.TwoLetterISORegionName, $"{region.TwoLetterISORegionName} - {region.EnglishName}"));
            any = true;
        }

        if (!any)
        {
            Countries.Add(new CountryOption("BG", "BG - Bulgaria"));
            Countries.Add(new CountryOption("US", "US - United States"));
        }
    }
}
