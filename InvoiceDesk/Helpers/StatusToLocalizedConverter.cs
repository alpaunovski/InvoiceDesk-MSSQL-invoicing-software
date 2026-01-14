using System;
using System.Globalization;
using System.Windows.Data;
using InvoiceDesk.Models;
using InvoiceDesk.Resources;

namespace InvoiceDesk.Helpers;

public class StatusToLocalizedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not InvoiceStatus status)
        {
            return value;
        }

        return status switch
        {
            InvoiceStatus.Draft => Strings.StatusDraft,
            InvoiceStatus.Issued => Strings.StatusIssued,
            InvoiceStatus.Paid => Strings.StatusPaid,
            InvoiceStatus.Cancelled => Strings.StatusCancelled,
            _ => status.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
