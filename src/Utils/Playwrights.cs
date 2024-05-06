using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using SenluoScraper.Utils.Extensions;
using static SenluoScraper.Utils.Objects;

namespace SenluoScraper.Utils;

internal static class Playwrights {
    private static async Task<Dictionary<string, object>> GetFirefoxConfig(ILogger logger) {
        Dictionary<string, object> CastJson(Dictionary<string, JsonElement> json) {
            var result = new Dictionary<string, object>();
            foreach (var (key, value) in json) {
                object castedValue = value.ValueKind switch {
                    JsonValueKind.String => RequireNonNull(value.GetString(), "Fail to parse json string value"),
                    JsonValueKind.Number => value.GetInt32(),
                    JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
                    _ => throw new NotSupportedException()
                };
                result.Add(key, castedValue);
            }

            return result;
        }

        async Task<Dictionary<string, JsonElement>> GetDefaultConfig() {
            logger.LogDebug("Firefox preference not found, fallback to embedded JSON");
            var assembly = Assembly.GetExecutingAssembly();
            await using var stream =
                assembly.GetManifestResourceStream("SenluoScraper.resources.firefox-user-pref.json");
            var jsonElement = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonElement>>(
                RequireNonNull(stream, "Fail to create stream from embedded JSON"));
            return RequireNonNull(jsonElement, "Fail to deserialize embedded JSON");
        }

        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(configDir)) {
            var json = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "firefox/user-pref.json");
            if (File.Exists(json)) {
                logger.LogDebug("Firefox preference found");
                return CastJson(await json.ReadJson<Dictionary<string, JsonElement>>());
            } else {
                return CastJson(await GetDefaultConfig());
            }
        } else {
            return CastJson(await GetDefaultConfig());
        }
    }

    internal static async Task RunInBrowser(
        string url,
        Func<IBrowserContext, IPage, Task> continuation,
        Func<IBrowserContext, Task> contextHandler,
        ILogger logger,
        uint maxRetryTimes = 3
    ) {
        uint times = 0;
        while (true) {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Firefox.LaunchAsync(
                new BrowserTypeLaunchOptions {
                    FirefoxUserPrefs = await GetFirefoxConfig(logger),
                    Headless = !logger.IsEnabled(LogLevel.Debug)
                });
            await using var context = await browser.NewContextAsync();
            await contextHandler(context);

            var page = await context.NewPageAsync();
            try {
                await page.GotoAsync(url);
                logger.LogDebug("Page '{Url}' loaded successfully", page.Url);
                await continuation(context, page);
                return;
            } catch (TimeoutException) {
                times += 1;
                if (times > maxRetryTimes) {
                    throw;
                } else {
                    logger.LogWarning(
                        "Fail to load page '{Url}', attempt to restart browser [{Times}/{MaxTimes}]",
                        url,
                        times,
                        maxRetryTimes
                    );
                }
            } finally {
                await page.CloseAsync();
            }
        }
    }

    internal static async Task RunInBrowser(
        string url,
        Func<IBrowserContext, IPage, Task> continuation,
        uint maxRetryTimes = 3
    ) {
        await RunInBrowser(url, continuation, _ => Task.CompletedTask, NullLogger.Instance, maxRetryTimes);
    }

    internal static async Task RunInBrowser(
        string url,
        Func<IBrowserContext, IPage, Task> continuation,
        Func<IBrowserContext, Task> contextHandler,
        uint maxRetryTimes = 3
    ) {
        await RunInBrowser(url, continuation, contextHandler, NullLogger.Instance, maxRetryTimes);
    }

    internal static async Task RunInBrowser(
        string url,
        Func<IBrowserContext, IPage, Task> continuation,
        ILogger logger,
        uint maxRetryTimes = 3
    ) {
        await RunInBrowser(url, continuation, _ => Task.CompletedTask, logger, maxRetryTimes);
    }
}
