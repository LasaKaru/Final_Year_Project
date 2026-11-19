using System.Security.Claims;
using BolaLab.Api.Domain;
using Microsoft.AspNetCore.Authorization;
namespace BolaLab.Api.Authorization;

public sealed class DocumentEditRequirement : IAuthorizationRequirement { }

public sealed class DocumentEditHandler : AuthorizationHandler<DocumentEditRequirement, Document>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, DocumentEditRequirement requirement, Document resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null &&
            (resource.OwnerUserId == userId || resource.Shares.Any(s => s.UserId == userId && s.CanEdit)))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
