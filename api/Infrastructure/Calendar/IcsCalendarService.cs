using System.Text;
using Application.Processes;

namespace Infrastructure.Calendar;

/// <summary>
/// Builds minimal RFC-5545 iCalendar (.ics) payloads. Used to attach a calendar hold to leave
/// approval emails so the recipient can add it to their calendar in one click — no external
/// calendar API or account required, which keeps the demo self-contained.
/// </summary>
public class IcsCalendarService : ICalendarService
{
    public byte[] BuildHold(string summary, string description, DateOnly start, DateOnly end, string? organizerEmail)
    {
        // All-day VEVENT: DTEND is exclusive in iCalendar, so add a day to make [start, end] inclusive.
        var dtStart = start.ToString("yyyyMMdd");
        var dtEnd = end.AddDays(1).ToString("yyyyMMdd");
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var uid = $"{Guid.NewGuid():N}@miriani";

        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//Miriani//BPA//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:PUBLISH\r\n");
        sb.Append("BEGIN:VEVENT\r\n");
        sb.Append($"UID:{uid}\r\n");
        sb.Append($"DTSTAMP:{stamp}\r\n");
        sb.Append($"DTSTART;VALUE=DATE:{dtStart}\r\n");
        sb.Append($"DTEND;VALUE=DATE:{dtEnd}\r\n");
        sb.Append($"SUMMARY:{Escape(summary)}\r\n");
        sb.Append($"DESCRIPTION:{Escape(description)}\r\n");
        if (!string.IsNullOrWhiteSpace(organizerEmail))
            sb.Append($"ORGANIZER:mailto:{organizerEmail}\r\n");
        sb.Append("TRANSP:OPAQUE\r\n");
        sb.Append("END:VEVENT\r\n");
        sb.Append("END:VCALENDAR\r\n");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Escape iCalendar text per RFC 5545 (backslash, comma, semicolon, newline).</summary>
    private static string Escape(string s) => (s ?? "")
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
        .Replace("\r\n", "\\n").Replace("\n", "\\n");
}
