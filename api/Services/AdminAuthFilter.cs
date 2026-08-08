using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api.Services;

/// <summary>
/// docs/11-auth.md Bölüm 1.9/0.1: önceden paylaşılan bir X-Admin-Key header'ıyla
/// çalışıyordu (bkz. AdminConfig.cs) — artık gerçek bir rol-tabanlı kimlik doğrulama
/// var, bu yüzden bu filtre gerçek Player.Role == Admin kontrolüne taşındı. İki
/// paralel admin yetkilendirme sistemi bırakılmaz (Bölüm 0.1) — AdminConfig.AccessKey
/// artık okunmuyor, bilinçli olarak kaldırılmadı (01-workflow-rules.md Bölüm 0.2
/// "mevcut dosyayı yeniden düzenleme"), yalnızca bu görevin raporunda ölü alan
/// olarak not düşülür. JWT Bearer authentication middleware (Program.cs) bu filtreden
/// önce çalışır, context.HttpContext.User bu noktada zaten doldurulmuş olur.
/// </summary>
public class AdminAuthFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Admin"))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}

public class AdminAuthAttribute : TypeFilterAttribute
{
    public AdminAuthAttribute() : base(typeof(AdminAuthFilter))
    {
    }
}
