using Microsoft.AspNetCore.Http;
using KaizenWebApp.Services;
using System.Threading.Tasks;

namespace KaizenWebApp.Middleware
{
    public class SystemMaintenanceMiddleware
    {
        private readonly RequestDelegate _next;

        public SystemMaintenanceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISystemService systemService)
        {
            // Skip maintenance check for admin login, admin panel, and static files
            if (IsExemptFromMaintenanceCheck(context))
            {
                await _next(context);
                return;
            }

            // Check if system is in maintenance mode
            var isOffline = await systemService.IsSystemOfflineAsync();
            
            if (isOffline)
            {
                // Allow admin users to access the system during maintenance
                var username = context.User.Identity?.Name;
                if (username?.ToLower() == "admin")
                {
                    await _next(context);
                    return;
                }

                // For all other users, redirect to maintenance page
                if (!context.Request.Path.StartsWithSegments("/Home/Maintenance"))
                {
                    context.Response.Redirect("/Home/Maintenance");
                    return;
                }
            }

            await _next(context);
        }

        private bool IsExemptFromMaintenanceCheck(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();
            
            // Exempt admin login, admin panel, static files, and maintenance page
            return path != null && (
                path.StartsWith("/account/login") ||
                path.StartsWith("/admin/") ||
                path.StartsWith("/home/maintenance") ||
                path.StartsWith("/lib/") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/images/") ||
                path.StartsWith("/favicon") ||
                path.StartsWith("/_framework/") ||
                path.StartsWith("/_blazor/")
            );
        }
    }
}
