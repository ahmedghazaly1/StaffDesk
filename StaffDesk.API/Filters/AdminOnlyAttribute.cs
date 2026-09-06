using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace StaffDesk.API.Filters;

public class AdminOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var isAdmin = user.IsInRole("Admin");
        
        if (!isAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}