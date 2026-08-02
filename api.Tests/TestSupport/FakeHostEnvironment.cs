using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace api.Tests.TestSupport;

/// <summary>MapProvider'ın Data/map.json'ı okuyabilmesi için gerçek api projesinin kök dizinini gösteren sahte host ortamı.</summary>
public class FakeHostEnvironment : IHostEnvironment
{
    public string ContentRootPath { get; set; } = ResolveApiContentRoot();
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "api.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;

    private static string ResolveApiContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "api", "api.csproj")))
        {
            dir = dir.Parent;
        }

        return dir is not null
            ? Path.Combine(dir.FullName, "api")
            : throw new InvalidOperationException("api.csproj bulunamadı (test ortamı Data/map.json'a erişemiyor).");
    }
}
