using System.ComponentModel.DataAnnotations;

namespace LocalHub.Application.Common;

// Rejects more decimal places than the column allows, so PostgreSQL never rounds the value silently.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxDecimalPlacesAttribute : ValidationAttribute
{
    private readonly int _decimalPlaces;

    public MaxDecimalPlacesAttribute(int decimalPlaces)
    {
        _decimalPlaces = decimalPlaces;
    }

    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            return true;
        }

        decimal number = (decimal)value;
        return decimal.Round(number, _decimalPlaces) == number;
    }
}