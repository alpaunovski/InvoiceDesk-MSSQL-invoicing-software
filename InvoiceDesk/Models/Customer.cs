using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InvoiceDesk.Models;

public class Customer : INotifyPropertyChanged
{
    private int _id;
    private int _companyId;
    private string _name = string.Empty;
    private string _vatNumber = string.Empty;
    private string? _eik;
    private string _countryCode = string.Empty;
    private bool _isVatRegistered;
    private string _address = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;

    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public int CompanyId
    {
        get => _companyId;
        set => SetField(ref _companyId, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string VatNumber
    {
        get => _vatNumber;
        set => SetField(ref _vatNumber, value);
    }

    public string? Eik
    {
        get => _eik;
        set => SetField(ref _eik, value);
    }

    public string CountryCode
    {
        get => _countryCode;
        set => SetField(ref _countryCode, value);
    }

    public bool IsVatRegistered
    {
        get => _isVatRegistered;
        set => SetField(ref _isVatRegistered, value);
    }

    public string Address
    {
        get => _address;
        set => SetField(ref _address, value);
    }

    public string Email
    {
        get => _email;
        set => SetField(ref _email, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetField(ref _phone, value);
    }

    public Company? Company { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
