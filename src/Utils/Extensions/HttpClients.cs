using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SenluoScraper.Utils.Extensions;

internal static class HttpClientExtensions {
    internal static async Task Download(
        this HttpClient client,
        string url,
        string filePath,
        ILogger logger,
        uint maxRetryTimes = 3
    ) {
        uint times = 0;
        while (true) {
            try {
                var buffer = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(filePath, buffer);
                return;
            } catch (HttpRequestException) {
                times += 1;
                if (times > maxRetryTimes) {
                    throw;
                } else {
                    logger.LogWarning(
                        "Fail to download '{Url}', attempt to retry [{Times}/{MaxTimes}]",
                        url,
                        times,
                        maxRetryTimes
                    );
                }
            }
        }
    }

    internal static async Task Download(
        this HttpClient client,
        string url,
        string filePath,
        uint maxRetryTimes = 3
    ) {
        await client.Download(url, filePath, NullLogger.Instance, maxRetryTimes);
    }
}
