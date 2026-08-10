using Microsoft.AspNetCore.Authorization;

namespace LedgerForge.Web.Security;

public sealed class ModuleAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuthorizationService authorizationService)
    {
        if (context.User.Identity?.IsAuthenticated == true && TryResolveModule(context.Request.Path, out var module))
        {
            var authorization = await authorizationService.AuthorizeAsync(
                context.User,
                AuthorizationPolicies.ModulePolicy(module, ModuleAccessLevel.View));
            if (!authorization.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }

    internal static bool TryResolveModule(PathString path, out LedgerForgeModule module)
    {
        var value = path.Value ?? string.Empty;
        module = value switch
        {
            "/" => LedgerForgeModule.Dashboard,
            _ when Starts(value, "/budget") || Starts(value, "/actuals") => LedgerForgeModule.Budget,
            _ when Starts(value, "/purchase-orders") || Starts(value, "/invoices") => LedgerForgeModule.Procurement,
            _ when Starts(value, "/vendors") => LedgerForgeModule.Vendors,
            _ when Starts(value, "/contracts") => LedgerForgeModule.Contracts,
            _ when Starts(value, "/renewals") => LedgerForgeModule.Renewals,
            _ when Starts(value, "/documents") => LedgerForgeModule.Documents,
            _ when Starts(value, "/approvals") => LedgerForgeModule.Approvals,
            _ when Starts(value, "/reports") || Starts(value, "/exports") => LedgerForgeModule.Reports,
            _ when Starts(value, "/fiscal-years") => LedgerForgeModule.FiscalYears,
            _ when Starts(value, "/imports") => LedgerForgeModule.Imports,
            _ when Starts(value, "/audit") => LedgerForgeModule.Audit,
            _ when Starts(value, "/admin") || Starts(value, "/work") => LedgerForgeModule.Administration,
            _ => default
        };

        return value == "/" ||
               Starts(value, "/budget") || Starts(value, "/actuals") || Starts(value, "/purchase-orders") ||
               Starts(value, "/invoices") || Starts(value, "/vendors") || Starts(value, "/contracts") ||
               Starts(value, "/renewals") || Starts(value, "/documents") || Starts(value, "/approvals") ||
               Starts(value, "/reports") || Starts(value, "/exports") || Starts(value, "/fiscal-years") ||
               Starts(value, "/imports") || Starts(value, "/audit") || Starts(value, "/admin") || Starts(value, "/work");
    }

    private static bool Starts(string path, string prefix)
        => string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
}
