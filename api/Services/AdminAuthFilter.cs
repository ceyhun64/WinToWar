using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace api.Services;

/// <summary>Bkz. AdminConfig.cs gerekçesi — X-Admin-Key header'ı paylaşılan anahtarla eşleşmezse 401 döner.</summary>
public class AdminAuthFilter : IAsyncActionFilter
{
    private const string HeaderName = "X-Admin-Key";
    private readonly AdminConfig _config;

    public AdminAuthFilter(IOptions<AdminConfig> config)
    {
        _config = config.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            providedKey.Count == 0 ||
            !CryptographicallyEqual(providedKey[0] ?? string.Empty, _config.AccessKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }

    private static bool CryptographicallyEqual(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
}

public class AdminAuthAttribute : TypeFilterAttribute
{
    public AdminAuthAttribute() : base(typeof(AdminAuthFilter))
    {
    }
}
