using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceDesk.Models;
using InvoiceDesk.Services;
using InvoiceDesk.Resources;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace InvoiceDesk.ViewModels;

public partial class CompanyManagementViewModel : ObservableObject
{
    private readonly CompanyService _companyService;
    private readonly ILanguageService _languageService;

    [ObservableProperty]
    private ObservableCollection<Company> companies = new();

    [ObservableProperty]
    private Company? selectedCompany;

    public ObservableCollection<CountryOption> Countries { get; } = new();

    public CompanyManagementViewModel(CompanyService companyService, ILanguageService languageService)
    {
        _companyService = companyService;
        _languageService = languageService;
        _languageService.CultureChanged += OnCultureChanged;
        OnPropertyChanged(nameof(IsBulgarianUi));
        LoadCountryOptions();
    }

    public bool IsBulgarianUi => string.Equals(_languageService.CurrentCulture.TwoLetterISOLanguageName, "bg", StringComparison.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        var list = await _companyService.GetCompaniesAsync();
        Companies = new ObservableCollection<Company>(list);
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

        var anyAdded = false;
        foreach (var region in regions)
        {
            Countries.Add(new CountryOption(region.TwoLetterISORegionName, $"{region.TwoLetterISORegionName} - {region.EnglishName}"));
            anyAdded = true;
        }

        if (!anyAdded)
        {
            Countries.Add(new CountryOption("BG", "BG - Bulgaria"));
            Countries.Add(new CountryOption("US", "US - United States"));
        }
    }

    [RelayCommand]
    private void AddCompany()
    {
        Companies.Add(new Company
        {
            Name = "",
            VatNumber = "",
            CountryCode = "",
            Address = "",
            BankIban = "",
            BankBic = "",
            NextInvoiceNumber = 1
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        foreach (var company in Companies)
        {
            await _companyService.SaveAsync(company);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedCompany == null)
        {
            return;
        }

        var target = SelectedCompany;
        if (target.Id != 0)
        {
            try
            {
                await _companyService.DeleteAsync(target.Id);
            }
            catch (InvalidOperationException ex)
            {
                // Surface user-friendly errors (e.g., cannot delete with issued invoices).
                MessageBox.Show(ex.Message, Strings.Delete, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        Companies.Remove(target);
    }

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        OnPropertyChanged(nameof(IsBulgarianUi));
    }
}
