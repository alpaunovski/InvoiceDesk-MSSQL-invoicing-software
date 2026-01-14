namespace InvoiceDesk.Models;

public sealed class CountryOption
{
    public string Code { get; }
    public string Label { get; }

    public CountryOption(string code, string label)
    {
        Code = code;
        Label = label;
    }
}
