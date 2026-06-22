using System.Globalization;
using System.Text;
using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public sealed class ReportExportService
{
    private readonly TicketRepository _tickets;

    public ReportExportService(TicketRepository tickets)
    {
        _tickets = tickets;
    }

    public async Task<ReportSummaryResponse> BuildSummaryAsync(int userId, string role)
    {
        var analytics = await _tickets.GetDashboardAnalyticsAsync(userId, role);
        var tickets = (await _tickets.GetTicketsAsync(userId, role)).ToList();

        return new ReportSummaryResponse(
            DateTime.UtcNow,
            analytics.TotalTickets,
            analytics.OpenTickets,
            analytics.InProgressTickets,
            tickets.Count(ticket => ticket.StatusName == "Pending"),
            analytics.ResolvedTickets,
            tickets.Count(ticket => ticket.StatusName == "Closed"),
            analytics.CriticalTickets,
            analytics.TicketsByStatus,
            analytics.TicketsByCategory,
            analytics.TicketsByPriority,
            analytics.TicketsByAgent,
            tickets.Take(10).ToList());
    }

    public async Task<byte[]> BuildExcelAsync(int userId, string role)
    {
        var report = await BuildSummaryAsync(userId, role);
        var xml = new StringBuilder();

        xml.AppendLine("""<?xml version="1.0"?>""");
        xml.AppendLine("""<?mso-application progid="Excel.Sheet"?>""");
        xml.AppendLine("""<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">""");
        xml.AppendLine("<Worksheet ss:Name=\"Report Summary\"><Table>");
        AddRow(xml, "Metric", "Value");
        AddRow(xml, "Generated At", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        AddRow(xml, "Total Tickets", report.TotalTickets.ToString(CultureInfo.InvariantCulture));
        AddRow(xml, "Open Tickets", report.OpenTickets.ToString(CultureInfo.InvariantCulture));
        AddRow(xml, "In Progress Tickets", report.InProgressTickets.ToString(CultureInfo.InvariantCulture));
        AddRow(xml, "Pending Tickets", report.PendingTickets.ToString(CultureInfo.InvariantCulture));
        AddRow(xml, "Resolved Tickets", report.ResolvedTickets.ToString(CultureInfo.InvariantCulture));
        AddRow(xml, "Closed Tickets", report.ClosedTickets.ToString(CultureInfo.InvariantCulture));
        AddRow(xml, "Critical Tickets", report.CriticalTickets.ToString(CultureInfo.InvariantCulture));
        xml.AppendLine("</Table></Worksheet>");

        xml.AppendLine("<Worksheet ss:Name=\"Recent Tickets\"><Table>");
        AddRow(xml, "Reference", "Title", "Category", "Priority", "Status", "Created By", "Assigned To", "Created Date");
        foreach (var ticket in report.RecentTickets)
        {
            AddRow(
                xml,
                ticket.TicketNumber,
                ticket.Title,
                ticket.CategoryName,
                ticket.PriorityName,
                ticket.StatusName,
                ticket.CreatedByName,
                ticket.AssignedAgentName ?? "Unassigned",
                ticket.CreatedDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        }

        xml.AppendLine("</Table></Worksheet>");
        xml.AppendLine("</Workbook>");

        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    public async Task<byte[]> BuildPdfAsync(int userId, string role)
    {
        var report = await BuildSummaryAsync(userId, role);
        var lines = new List<string>
        {
            "IT Help Desk Report",
            $"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC",
            "",
            $"Total Tickets: {report.TotalTickets}",
            $"Open Tickets: {report.OpenTickets}",
            $"In Progress Tickets: {report.InProgressTickets}",
            $"Pending Tickets: {report.PendingTickets}",
            $"Resolved Tickets: {report.ResolvedTickets}",
            $"Closed Tickets: {report.ClosedTickets}",
            $"Critical Tickets: {report.CriticalTickets}",
            "",
            "Recent Tickets",
        };

        foreach (var ticket in report.RecentTickets.Take(12))
        {
            lines.Add($"{ticket.TicketNumber} | {ticket.Title} | {ticket.PriorityName} | {ticket.StatusName}");
        }

        return BuildSimplePdf(lines);
    }

    private static void AddRow(StringBuilder xml, params string[] values)
    {
        xml.AppendLine("<Row>");
        foreach (var value in values)
        {
            xml.Append("<Cell><Data ss:Type=\"String\">");
            xml.Append(System.Security.SecurityElement.Escape(value));
            xml.AppendLine("</Data></Cell>");
        }

        xml.AppendLine("</Row>");
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 14 Tf");
        content.AppendLine("50 790 Td");

        foreach (var line in lines)
        {
            content.Append('(');
            content.Append(EscapePdfText(line));
            content.AppendLine(") Tj");
            content.AppendLine("0 -22 Td");
        }

        content.AppendLine("ET");

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream",
        };

        var pdf = new StringBuilder();
        var offsets = new List<int> { 0 };
        pdf.AppendLine("%PDF-1.4");

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{index + 1} 0 obj");
            pdf.AppendLine(objects[index]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");

        foreach (var offset in offsets.Skip(1))
        {
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string EscapePdfText(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
