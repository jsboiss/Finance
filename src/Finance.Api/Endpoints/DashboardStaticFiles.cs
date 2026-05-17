namespace Finance.Api.Endpoints;

using Finance.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.FileProviders;

public static class DashboardStaticFiles
{
    public static WebApplication UseDashboardProtection(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (IsPublicPath(context.Request.Path))
            {
                await next();
                return;
            }

            var result = await context.AuthenticateAsync(OwnerDashboardAuthenticationHandler.SchemeName);
            if (!result.Succeeded)
            {
                await context.ChallengeAsync(OwnerDashboardAuthenticationHandler.SchemeName);
                return;
            }

            context.User = result.Principal!;
            await next();
        });

        return app;
    }

    public static WebApplication MapDashboardStaticFiles(this WebApplication app)
    {
        var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (!Directory.Exists(webRoot))
        {
            return app;
        }

        var fileProvider = new PhysicalFileProvider(webRoot);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
        return app;
    }

    private static bool IsPublicPath(PathString path)
    {
        return path.StartsWithSegments("/api/v1") ||
            path.StartsWithSegments("/webhooks") ||
            path.StartsWithSegments("/openapi");
    }
}
