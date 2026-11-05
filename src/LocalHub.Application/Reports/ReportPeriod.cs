using System.Globalization;

using LocalHub.Application.Common.Exceptions;

namespace LocalHub.Application.Reports;

public record ReportPeriod(DateOnly? StartDate, DateOnly? EndDate)
{
    public const string StartDateField = "startDate";
    public const string EndDateField = "endDate";
    public const string InvalidDateMessage = "Indique uma data válida no formato AAAA-MM-DD.";
    public const string InvertedMessage = "A data inicial não pode ser posterior à data final.";

    private const string DateFormat = "yyyy-MM-dd";

    public static ReportPeriod Parse(string? startDate, string? endDate)
    {
        var errors = new Dictionary<string, string[]>();
        var start = ParseDate(startDate, StartDateField, errors);
        var end = ParseDate(endDate, EndDateField, errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (start != null && end != null && start > end)
        {
            throw new ValidationException(InvertedMessage);
        }

        return new ReportPeriod(start, end);
    }

    public DateTimeOffset? From => StartDate == null ? null : StartOfDay(StartDate.Value);

    // Exclusive limit (the day after the end date); DateOnly.MaxValue has no next day.
    public DateTimeOffset? Until => EndDate == null || EndDate.Value == DateOnly.MaxValue
        ? null
        : StartOfDay(EndDate.Value.AddDays(1));

    private static DateOnly? ParseDate(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors[field] = new[] { InvalidDateMessage };
            return null;
        }

        return date;
    }

    // PostgreSQL only accepts UTC for timestamptz.
    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}