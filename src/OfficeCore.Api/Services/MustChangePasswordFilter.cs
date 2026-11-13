using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public sealed class MustChangePasswordFilter : IAsyncActionFilter
{
    private readonly AppDbContext _context;

    public MustChangePasswordFilter(AppDbContext context)
    {
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            var mustChange = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.MustChangePassword)
                .FirstOrDefaultAsync();

            if (mustChange)
            {
                context.Result = new ObjectResult(new
                {
                    message = "You must change your temporary password before accessing workspace resources.",
                    requirePasswordChange = true
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        await next();
    }
}
