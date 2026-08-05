using Microsoft.Extensions.DependencyInjection;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public static class CoreDownloaderServiceCollectionExtensions
{
    public static IServiceCollection AddCoreDownloader(this IServiceCollection services)
    {
        services.AddSingleton<PaperMcSource>();
        services.AddSingleton<PurpurMcSource>();
        services.AddSingleton<McJarFilesSource>();
        services.AddSingleton<BmclApiMirrorSource>();
        services.AddSingleton<CoreDownloadService>(sp => new CoreDownloadService(
            new List<ICoreDownloadSource>
            {
                sp.GetRequiredService<PaperMcSource>(),
                sp.GetRequiredService<PurpurMcSource>(),
                sp.GetRequiredService<McJarFilesSource>(),
                sp.GetRequiredService<BmclApiMirrorSource>(),
            }));
        return services;
    }
}
