using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MajlisManagement.Data;

namespace MajlisManagement.Middleware;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission) => _permission = permission;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.IsInRole("Admin")) { await next(); return; }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        { context.Result = new ForbidResult(); return; }

        var p = await db.UserPermissions.FirstOrDefaultAsync(x => x.UserId == userId);

        var allowed = _permission switch
        {
            "ViewDashboard"     => p?.ViewDashboard     ?? false,
            "ViewBookings"      => p?.ViewBookings      ?? false,
            "CreateBookings"    => p?.CreateBookings    ?? false,
            "ConfirmBookings"   => p?.ConfirmBookings   ?? false,
            "DeleteBookings"    => p?.DeleteBookings    ?? false,
            "ViewPayments"      => p?.ViewPayments      ?? false,
            "ManagePayments"    => p?.ManagePayments    ?? false,
            "ViewMembers"       => p?.ViewMembers       ?? false,
            "ManageMembers"     => p?.ManageMembers     ?? false,
            "ViewAllComplaints" => p?.ViewAllComplaints ?? false,
            "RespondComplaints" => p?.RespondComplaints ?? false,
            "ViewReports"       => p?.ViewReports       ?? false,
            _ => false
        };

        if (!allowed) { context.Result = new ForbidResult(); return; }

        await next();
    }
}
