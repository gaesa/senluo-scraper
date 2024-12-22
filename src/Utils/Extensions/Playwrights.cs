using Microsoft.Playwright;

namespace SenluoScraper.Utils.Extensions;

internal static class PageExtensions {
    internal static async Task ScrollBy(this IPage page, uint y) {
        await page.EvaluateAsync($"window.scrollBy(0, {y})");
    }

    internal static async Task ScrollTo(this IPage page, uint y) {
        await page.EvaluateAsync($"window.scrollTo(0, {y})");
    }

    internal static async Task<uint> GetTotalHeight(this IPage page) {
        return await page.EvaluateAsync<uint>("window.scrollMaxY");
    }

    internal static async Task<uint> GetVerticalPosition(this IPage page) {
        return Convert.ToUInt32(await page.EvaluateAsync<float>("window.scrollY"));
    }

    private static async Task<string> GetUserAgent(this IPage page) {
        return await page.EvaluateAsync<string>("navigator.userAgent");
    }

    // This is based on HTTP/1.1, see also: https://github.com/microsoft/playwright/issues/23176
    // So it is expected to fail for some websites
    internal static async Task Download(this IPage page, string url, string directory, string fileName) {
        var response = await page.APIRequest.GetAsync(
            url, new APIRequestContextOptions {
                Headers = new KeyValuePair<string, string>("User-Agent", await page.GetUserAgent()).Once()
            });
        var buffer = await response.BodyAsync();
        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(filePath, buffer);
    }

    internal static async Task<HttpClient> NewHttpClient(this IPage page) {
        var client = new HttpClient();
        client.DefaultRequestVersion = new Version(2, 0);
        client.DefaultRequestHeaders.Add("User-Agent", await page.GetUserAgent());
        return client;
    }
}

internal static class LocatorExtensions {
    private static async Task Hide(this ILocator locator) {
        await locator.EvaluateAsync("ele => ele.style.display = 'none'");
    }

    internal static async Task HideAll(this IEnumerable<ILocator> locators) {
        await locators
            .FilterForEachAsync(
                async locator => await locator.IsVisibleAsync(),
                async locator => await locator.Hide()
            );
    }
}

internal static class BrowserContextExtensions {
    // This is very robust, but also very expansive
    internal static async Task DownloadInNewTab(
        this IBrowserContext context,
        string url,
        string filePath
    ) {
        var page = await context.NewPageAsync();
        page.Response += async (_, response) => {
            if (response.Status < 300) { // skip potential redirection
                var buffer = await response.BodyAsync();
                await File.WriteAllBytesAsync(filePath, buffer);
            }
        };
        await page.GotoAsync(url);
        await page.CloseAsync();
    }
}
