using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Common;

// Rejects a decimal with more decimal places than the column allows (for example 12.345 in numeric(8,2)), so that
// PostgreSQL never rounds the value silently. A null value is accepted: [Required] checks it.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxDecimalPlacesAttribute(int decimalPlaces) : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not decimal number)
        {
            return true;
        }

        return decimal.Round(number, decimalPlaces) == number;
    }
}
