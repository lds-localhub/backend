using System.Globalization;

using LocalHub.Application.Common.Exceptions;

namespace LocalHub.Application.Reports;

// Period of the reports (RF-F013-02, RF-F014-02): optional dates, both included, compared with the creation date of the
// service in Portugal. Without dates, the report covers all the activity.
public record ReportPeriod(DateOnly? StartDate, DateOnly? EndDate)
{
    public const string InvalidDateMessage = "As datas do período têm de estar no formato AAAA-MM-DD.";
    public const string InvertedMessage = "A data inicial não pode ser posterior à data final.";

    private const string DateFormat = "yyyy-MM-dd";

    // The day of a service is the day in Portugal, as in the requested dates of E20 (RF-F010-01, rule 5).
    private static readonly TimeZoneInfo Portugal = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    // The values of the query string. An invalid date or an inverted period is a global error of the operation
    // (API documentation, E33 and E34), so the 400 has only "detail".
    public static ReportPeriod Parse(string? startDate, string? endDate)
    {
        var start = ParseDate(startDate);
        var end = ParseDate(endDate);
        if (start is not null && end is not null && start > end)
        {
            throw new ValidationException(InvertedMessage);
        }

        return new ReportPeriod(start, end);
    }

    // First instant of the start date in Portugal, in UTC; null without start date.
    public DateTimeOffset? From => StartDate is null ? null : StartOfDayUtc(StartDate.Value);

    // First instant of the day after the end date in Portugal, in UTC (exclusive limit); null without end date.
    public DateTimeOffset? Until => EndDate is null ? null : StartOfDayUtc(EndDate.Value.AddDays(1));

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ValidationException(InvalidDateMessage);
        }

        return date;
    }

    // PostgreSQL only accepts instants in UTC for timestamptz, so the offset is 0.
    private static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Portugal);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}