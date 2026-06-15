using System.Security.Claims;

namespace HelpDesk.Api.Controllers;

public static class CurrentUser
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId)
            ? userId
            : throw new InvalidOperationException("User ID claim is missing.");
    }

    public static string GetRole(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role)
            ?? throw new InvalidOperationException("Role claim is missing.");
    }
}
