namespace Finance.Api.Endpoints;

using Microsoft.Extensions.FileProviders;

public static class DashboardStaticFiles
{
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
}
