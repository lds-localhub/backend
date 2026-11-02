using System.Globalization;

using LocalHub.Application.Common.Exceptions;

namespace LocalHub.Application.Reports;

// Period of the reports (RF-F013-02, RF-F014-02): optional and independent dates, both included, compared with the
// creation date of the service in UTC (API documentation v04, section 4.3). Without dates, the report covers all the
// activity.
public record ReportPeriod(DateOnly? StartDate, DateOnly? EndDate)
{
    public const string StartDateField = "startDate";
    public const string EndDateField = "endDate";
    public const string InvalidDateMessage = "Indique uma data válida no formato AAAA-MM-DD.";
    public const string InvertedMessage = "A data inicial não pode ser posterior à data final.";

    private const string DateFormat = "yyyy-MM-dd";

    // The values of the query string. A date in an invalid format is an error of its parameter, in "errors" (section
    // 4.6); a start date after the end date is a global error of the operation, only in "detail" (E33, E34).
    public static ReportPeriod Parse(string? startDate, string? endDate)
    {
        var errors = new Dictionary<string, string[]>();
        var start = ParseDate(startDate, StartDateField, errors);
        var end = ParseDate(endDate, EndDateField, errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (start is not null && end is not null && start > end)
        {
            throw new ValidationException(InvertedMessage);
        }

        return new ReportPeriod(start, end);
    }

    // First instant of the start date in UTC; null without start date.
    public DateTimeOffset? From => StartDate is null ? null : StartOfDay(StartDate.Value);

    // First instant of the day after the end date in UTC (exclusive limit); null without end date. The last day that
    // DateOnly represents (9999-12-31) has no day after it, so it does not limit the period either.
    public DateTimeOffset? Until => EndDate is null || EndDate.Value == DateOnly.MaxValue
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

    // PostgreSQL only accepts instants in UTC for timestamptz, so the offset is 0.
    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}