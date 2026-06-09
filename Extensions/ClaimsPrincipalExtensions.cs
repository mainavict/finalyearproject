using System.Security.Claims;

namespace finalyearproject.Extensions;

public static class ClaimsPrincipalExtensions
{
    // This allows us to call User.GetUserId() in any controller!
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        // This looks for the exact claim we created in AuthService.cs
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            // If the token is invalid or missing the ID, throw an error
            throw new UnauthorizedAccessException("Invalid user token.");
        }
        
        return userId;
    }
}