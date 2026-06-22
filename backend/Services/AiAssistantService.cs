using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public sealed class AiAssistantService
{
    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hardware"] = new[] { "laptop", "desktop", "printer", "keyboard", "mouse", "screen", "monitor", "device", "hard disk" },
        ["Software"] = new[] { "application", "app", "software", "install", "crash", "error", "bug", "update" },
        ["Network"] = new[] { "wifi", "wi-fi", "internet", "network", "vpn", "router", "connection", "slow connection" },
        ["Email"] = new[] { "email", "outlook", "mail", "inbox", "send", "receive", "smtp" },
        ["Access Request"] = new[] { "access", "permission", "login", "password", "account", "role", "locked" },
    };

    public AiTicketAnalysisResponse AnalyzeTicket(AiTicketAnalysisRequest request)
    {
        var text = $"{request.Title} {request.Description}".ToLowerInvariant();
        var category = PickCategory(text);
        var priority = PickPriority(text);
        var summary = BuildSummary(request.Title, request.Description);
        var suggestion = BuildSuggestion(category, priority);
        var confidence = CalculateConfidence(text, category, priority);

        return new AiTicketAnalysisResponse(category, priority, summary, suggestion, confidence);
    }

    public AiChatResponse Chat(AiChatRequest request)
    {
        var message = request.Message.ToLowerInvariant();

        if (message.Contains("password", StringComparison.Ordinal) || message.Contains("login", StringComparison.Ordinal))
        {
            return new AiChatResponse(
                "This looks like an access issue. Ask the employee to confirm the account email, check whether the account is locked, then reset the password if needed.",
                new[] { "Check account status", "Verify role permissions", "Reset password", "Add an internal note" });
        }

        if (message.Contains("internet", StringComparison.Ordinal) || message.Contains("wifi", StringComparison.Ordinal) || message.Contains("network", StringComparison.Ordinal))
        {
            return new AiChatResponse(
                "This looks like a network issue. Confirm whether other users are affected, ask for the location, and test VPN or Wi-Fi connectivity before escalating.",
                new[] { "Ask for location", "Check Wi-Fi/VPN", "Test another device", "Escalate if many users are affected" });
        }

        if (message.Contains("email", StringComparison.Ordinal) || message.Contains("outlook", StringComparison.Ordinal))
        {
            return new AiChatResponse(
                "This looks like an email issue. Ask for the error message, test webmail, and check mailbox or Outlook profile settings.",
                new[] { "Ask for screenshot", "Test webmail", "Check mailbox access", "Recreate Outlook profile" });
        }

        return new AiChatResponse(
            "I can help you classify the issue, suggest a priority, prepare a ticket summary, and recommend the next troubleshooting step.",
            new[] { "Review ticket description", "Check category", "Confirm priority", "Add clear support notes" });
    }

    private static string PickCategory(string text)
    {
        var match = CategoryKeywords
            .Select(pair => new
            {
                Category = pair.Key,
                Score = pair.Value.Count(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)),
            })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();

        return match is not null && match.Score > 0 ? match.Category : "Other";
    }

    private static string PickPriority(string text)
    {
        if (ContainsAny(text, "critical", "urgent", "down", "outage", "all users", "cannot work", "security"))
        {
            return "Critical";
        }

        if (ContainsAny(text, "high", "blocked", "cannot login", "cannot access", "production", "manager"))
        {
            return "High";
        }

        if (ContainsAny(text, "slow", "sometimes", "intermittent", "printer", "request"))
        {
            return "Medium";
        }

        return "Low";
    }

    private static string BuildSummary(string title, string description)
    {
        var cleanDescription = string.IsNullOrWhiteSpace(description)
            ? "No description was provided."
            : description.Trim();

        if (cleanDescription.Length > 130)
        {
            cleanDescription = cleanDescription[..130].TrimEnd() + "...";
        }

        return $"{title.Trim()}: {cleanDescription}";
    }

    private static string BuildSuggestion(string category, string priority)
    {
        var firstStep = category switch
        {
            "Hardware" => "Check the physical device, cables, drivers, and whether the issue happens on another device.",
            "Software" => "Collect the exact error message, application version, recent updates, and reproduction steps.",
            "Network" => "Check whether the user is on Wi-Fi, LAN, or VPN and confirm if other employees are affected.",
            "Email" => "Test webmail, check Outlook profile settings, and ask for any bounce-back or error screenshot.",
            "Access Request" => "Verify the user identity, requested access level, manager approval, and account status.",
            _ => "Ask the employee for a screenshot, exact time of issue, affected device, and business impact.",
        };

        return priority == "Critical"
            ? $"{firstStep} Treat this as urgent and notify a manager if multiple users or a business-critical service are affected."
            : firstStep;
    }

    private static int CalculateConfidence(string text, string category, string priority)
    {
        var categoryScore = category == "Other" ? 25 : 45;
        var priorityScore = priority is "Critical" or "High" ? 25 : 15;
        var detailScore = text.Length > 80 ? 25 : 10;

        return Math.Min(95, categoryScore + priorityScore + detailScore);
    }

    private static bool ContainsAny(string text, params string[] words)
    {
        return words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
