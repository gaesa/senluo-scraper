using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SenluoScraper.Utils;

internal static class Logs {
    internal static ILogger CreateLogger(string categoryName) {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, false)
            .AddEnvironmentVariables().Build();

        var factory = LoggerFactory.Create(
            builder => {
                builder
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddConsole();
            });

        return factory.CreateLogger(categoryName);
    }
}
