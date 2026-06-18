namespace Application.Processes;

// ----- shared building blocks -----

/// <summary>One line item on an invoice. Totals are computed server-side, never trusted from the LLM.</summary>
public record InvoiceLine(string Description, double Quantity, double UnitPrice);

/// <summary>A fully-resolved invoice ready to render to PDF and/or email.</summary>
public record InvoiceDoc(
    string Client, string? ClientEmail, string Currency,
    IReadOnlyList<InvoiceLine> Items, string? Notes);

/// <summary>One reviewed contract clause and its risk finding.</summary>
public record ContractClause(string Clause, string Risk, string Finding);

/// <summary>A contract risk review ready to render to PDF and/or broadcast.</summary>
public record ContractReportDoc(
    string OverallRisk, IReadOnlyList<ContractClause> Clauses, IReadOnlyList<string> Recommendations);

// ----- integration service interfaces (implemented in Infrastructure) -----

/// <summary>Renders business documents to PDF bytes (QuestPDF under the hood).</summary>
public interface IPdfService
{
    byte[] RenderInvoice(InvoiceDoc doc);
    byte[] RenderContractReport(ContractReportDoc doc, string title);
}

/// <summary>Builds RFC-5545 iCalendar (.ics) payloads for calendar holds/invites.</summary>
public interface ICalendarService
{
    byte[] BuildHold(string summary, string description, DateOnly start, DateOnly end, string? organizerEmail);
}

/// <summary>Posts alerts to a chat channel (Slack/Teams/generic incoming webhook).</summary>
public interface INotificationService
{
    bool IsConfigured { get; }
    /// <summary>Returns true if delivered to a real webhook, false if no webhook is configured (no-op).</summary>
    Task<bool> NotifyAsync(string title, string text, CancellationToken ct = default);
}

/// <summary>Process-level settings, bound from the "Processes" config section.</summary>
public class ProcessOptions
{
    public const string SectionName = "Processes";
    public string DefaultManagerEmail { get; set; } = "";
}
