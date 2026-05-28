using Application.Common;
using Application.Email;
using Application.Jira;
using Application.Processes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

/// <summary>
/// End-to-end business-process automations. Each agent drafts a structured result in chat; the
/// user reviews it and triggers the real-world action here (send email, render PDF, create a
/// Jira ticket, broadcast an alert). Every action is human-in-the-loop and written to the audit
/// trail. Integrations degrade gracefully when not configured so live demos always run.
/// </summary>
[ApiController]
[Authorize]
[Route("api/processes")]
public class ProcessesController(
    IEmailService email,
    IJiraService jira,
    IPdfService pdf,
    ICalendarService calendar,
    INotificationService notifications,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IOptions<ProcessOptions> processOptions) : ControllerBase
{
    private readonly ProcessOptions _proc = processOptions.Value;

    /// <summary>Which integrations are wired up, so the UI can show only the actions that work.</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        email = email.IsConfigured,
        jira = jira.IsConfigured,
        jiraCanCreate = true, // always — falls back to a local placeholder key
        notifications = notifications.IsConfigured,
    });

    // ---------- 1. Leave approval: email the manager + attach an .ics hold ----------

    [HttpPost("leave/submit")]
    public async Task<IActionResult> SubmitLeave(LeaveSubmitRequest req, CancellationToken ct)
    {
        if (!email.IsConfigured) return BadRequest(new { error = "Outbound email is not configured." });
        var manager = FirstNonEmpty(req.ManagerEmail, _proc.DefaultManagerEmail);
        if (string.IsNullOrWhiteSpace(manager)) return BadRequest(new { error = "No manager email available." });

        var who = FirstNonEmpty(req.EmployeeName, "An employee");
        var subject = $"Leave approval request — {who} ({req.StartDate} to {req.EndDate})";
        var body =
            $"{who} has requested leave from {req.StartDate} to {req.EndDate} " +
            $"({req.DaysRequested} day(s)).\n\n" +
            (string.IsNullOrWhiteSpace(req.PolicyNote) ? "" : $"Policy check: {req.PolicyNote}\n\n") +
            (string.IsNullOrWhiteSpace(req.FormalLetter) ? "" : $"{req.FormalLetter}\n\n") +
            "A calendar hold is attached. Reply to approve or decline.";

        var attachments = new List<EmailAttachment>();
        if (TryDate(req.StartDate, out var s) && TryDate(req.EndDate, out var e) && e >= s)
        {
            var ics = calendar.BuildHold($"Leave — {who}", $"Leave request ({req.DaysRequested} days)", s, e, manager);
            attachments.Add(new EmailAttachment("leave-hold.ics", "text/calendar", ics));
        }

        await email.SendAsync(new SendEmailRequest(manager, subject, body), attachments, ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.leave.submit",
            $"{who} {req.StartDate}–{req.EndDate} → {manager}", ct);

        return Ok(new { sent = true, to = manager, calendarAttached = attachments.Count > 0 });
    }

    // ---------- 2. Invoice: render PDF (download) or email it to the client ----------

    [HttpPost("invoice/pdf")]
    public IActionResult InvoicePdf(InvoiceRequest req)
    {
        var bytes = pdf.RenderInvoice(req.ToDoc());
        return File(bytes, "application/pdf", $"invoice-{Slug(req.Client)}.pdf");
    }

    [HttpPost("invoice/email")]
    public async Task<IActionResult> InvoiceEmail(InvoiceRequest req, CancellationToken ct)
    {
        if (!email.IsConfigured) return BadRequest(new { error = "Outbound email is not configured." });
        if (string.IsNullOrWhiteSpace(req.ClientEmail) || !req.ClientEmail.Contains('@'))
            return BadRequest(new { error = "A valid client email is required." });

        var doc = req.ToDoc();
        var bytes = pdf.RenderInvoice(doc);
        var total = doc.Items.Sum(i => i.Quantity * i.UnitPrice);
        var body = $"Dear {doc.Client},\n\nPlease find your invoice attached " +
                   $"(total {total:0.00} {doc.Currency}).\n\nThank you for your business.\nMiriani";

        await email.SendAsync(
            new SendEmailRequest(req.ClientEmail!, $"Invoice from Miriani — {total:0.00} {doc.Currency}", body),
            new[] { new EmailAttachment($"invoice-{Slug(doc.Client)}.pdf", "application/pdf", bytes) }, ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.invoice.email",
            $"{doc.Client} {total:0.00} {doc.Currency} → {req.ClientEmail}", ct);

        return Ok(new { sent = true, to = req.ClientEmail });
    }

    // ---------- 3. IT helpdesk: create a Jira ticket ----------

    [HttpPost("ticket/create")]
    public async Task<IActionResult> CreateTicket(TicketRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Summary)) return BadRequest(new { error = "A ticket summary is required." });

        var desc = req.Description ?? "";
        if (!string.IsNullOrWhiteSpace(req.Priority)) desc = $"Priority: {req.Priority}\n\n{desc}";

        var created = await jira.CreateIssueAsync(
            new JiraNewIssue(req.Summary, desc, FirstNonEmpty(req.IssueType, "Task")), ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.ticket.create",
            $"{created.Key} — {req.Summary}{(created.Simulated ? " (simulated)" : "")}", ct);

        return Ok(created);
    }

    // ---------- 4. Onboarding: create Jira tasks + welcome email ----------

    [HttpPost("onboarding/provision")]
    public async Task<IActionResult> ProvisionOnboarding(OnboardingRequest req, CancellationToken ct)
    {
        var role = FirstNonEmpty(req.Role, "New employee");
        var who = FirstNonEmpty(req.EmployeeName, "the new hire");
        var phases = new (string Label, IReadOnlyList<string>? Items)[]
        {
            ("Day 1", req.Day_1), ("Week 1", req.Week_1), ("Month 1", req.Month_1),
        };

        var keys = new List<string>();
        var simulated = false;
        foreach (var (label, items) in phases)
        {
            if (items is null || items.Count == 0) continue;
            var desc = string.Join("\n", items.Select(i => $"- {i}"));
            var issue = await jira.CreateIssueAsync(
                new JiraNewIssue($"Onboarding {who} — {label} ({role})", desc, "Task"), ct);
            keys.Add(issue.Key);
            simulated |= issue.Simulated;
        }

        var emailSent = false;
        if (!string.IsNullOrWhiteSpace(req.NewHireEmail) && req.NewHireEmail.Contains('@') && email.IsConfigured)
        {
            var firstDay = req.Day_1 is { Count: > 0 } ? "\n\nYour first day:\n" + string.Join("\n", req.Day_1.Select(i => $"• {i}")) : "";
            var body = $"Hi {who},\n\nWelcome aboard as {role}! We're excited to have you." + firstDay +
                       "\n\nYour onboarding plan has been set up and your team will guide you through it.\n\nMiriani";
            await email.SendAsync(new SendEmailRequest(req.NewHireEmail!, $"Welcome to the team, {who}!", body), ct);
            emailSent = true;
        }

        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.onboarding.provision",
            $"{who} ({role}) — {keys.Count} task(s){(emailSent ? ", welcome email sent" : "")}", ct);

        return Ok(new { tickets = keys, simulated, emailSent, newHireEmail = req.NewHireEmail });
    }

    // ---------- 5. Contract review: render PDF (download) or broadcast an alert ----------

    [HttpPost("contract/report")]
    public IActionResult ContractReport(ContractRequest req)
    {
        var bytes = pdf.RenderContractReport(req.ToDoc(), FirstNonEmpty(req.Title, "Contract review"));
        return File(bytes, "application/pdf", "contract-risk-review.pdf");
    }

    [HttpPost("contract/alert")]
    public async Task<IActionResult> ContractAlert(ContractRequest req, CancellationToken ct)
    {
        var doc = req.ToDoc();
        var high = doc.Clauses.Count(c => string.Equals(c.Risk, "High", StringComparison.OrdinalIgnoreCase));
        var title = $"Contract review: {doc.OverallRisk} risk";
        var text = $"{FirstNonEmpty(req.Title, "A contract")} was reviewed — overall risk *{doc.OverallRisk}*, " +
                   $"{doc.Clauses.Count} clause(s), {high} high-risk finding(s).";

        var delivered = await notifications.NotifyAsync(title, text, ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.contract.alert",
            $"{doc.OverallRisk} risk, {high} high-risk{(delivered ? "" : " (no webhook configured)")}", ct);

        return Ok(new { alerted = delivered, channel = delivered ? "sent" : "no webhook configured" });
    }

    // ---------- helpers ----------

    private static string FirstNonEmpty(string? a, string b) => string.IsNullOrWhiteSpace(a) ? b : a!.Trim();

    private static bool TryDate(string? s, out DateOnly d) =>
        DateOnly.TryParse(s, out d);

    private static string Slug(string s) =>
        new string((s ?? "client").Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant() is { Length: > 0 } x ? x : "client";
}

// ----- request shapes (bound from the chat card's structured data) -----

public record LeaveSubmitRequest(
    string? EmployeeName, string? StartDate, string? EndDate, double DaysRequested,
    string? ManagerEmail, string? PolicyNote, string? FormalLetter);

public record InvoiceItemRequest(string Description, double Quantity, double UnitPrice);

public record InvoiceRequest(
    string Client, string? ClientEmail, string? Currency,
    List<InvoiceItemRequest>? Items, string? Notes)
{
    public InvoiceDoc ToDoc() => new(
        string.IsNullOrWhiteSpace(Client) ? "Client" : Client,
        ClientEmail,
        string.IsNullOrWhiteSpace(Currency) ? "GEL" : Currency!,
        (Items ?? new()).Select(i => new InvoiceLine(i.Description, i.Quantity, i.UnitPrice)).ToList(),
        Notes);
}

public record TicketRequest(string Summary, string? Description, string? Priority, string? IssueType);

public record OnboardingRequest(
    string? Role, string? EmployeeName, string? NewHireEmail,
    List<string>? Day_1, List<string>? Week_1, List<string>? Month_1);

public record ContractClauseRequest(string Clause, string Risk, string Finding);

public record ContractRequest(
    string? Title, string OverallRisk,
    List<ContractClauseRequest>? Clauses, List<string>? Recommendations)
{
    public ContractReportDoc ToDoc() => new(
        string.IsNullOrWhiteSpace(OverallRisk) ? "Unknown" : OverallRisk,
        (Clauses ?? new()).Select(c => new ContractClause(c.Clause, c.Risk, c.Finding)).ToList(),
        Recommendations ?? new());
}
