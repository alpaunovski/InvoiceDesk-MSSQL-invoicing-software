using System.ComponentModel;
using System.Runtime.CompilerServices;
using InvoiceDesk.Resources;

namespace InvoiceDesk.Helpers;

public class LocalizedStrings : INotifyPropertyChanged
{
    public string this[string key] => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RaiseCultureChanged()
    {
        OnPropertyChanged("Item[]");
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
