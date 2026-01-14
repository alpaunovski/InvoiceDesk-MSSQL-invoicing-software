namespace InvoiceDesk.Models
{
    public sealed class CultureOption
    {
        public string Code { get; }
        public string Label { get; }

        public CultureOption(string code, string label)
        {
            Code = code;
            Label = label;
        }
    }
}
