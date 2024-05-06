using System.Net;
using System.Text.RegularExpressions;
using CommandLine;
using Humanizer;
using Kurukuru;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SenluoScraper.Utils.Extensions;
using ShellProgressBar;
using static SenluoScraper.Utils.Objects;
using static SenluoScraper.Utils.Logs;
using static SenluoScraper.Utils.Playwrights;

namespace SenluoScraper;

// ReSharper disable once ClassNeverInstantiated.Global
internal class Cli(string url, string dir) {
    [Value(0, MetaName = "url", HelpText = "URL to download images from")]
    public string Url { get; } = url;

    [Value(1, MetaName = "directory", HelpText = "Directory to save images")]
    public string Dir { get; } = dir;
}

public static partial class Program {
    private static readonly ILogger Logger = CreateLogger("Program");

    private static async Task BlockRequest(IBrowserContext context) {
        string[] patterns = [
            "tianji.viagle.com",
            "www.googletagmanager.com",
            "platform-api.sharethis.com",
            "js.juicyads.com",
            "a.magsrv.com",
            "poweredby.jads.co",
            "a.pemsrv.com",
            "js.wpnsrv.com",
            "static.cloudflareinsights.com",
            "stats.viagle.com",
            "www.clarity.ms",
            "y.clarity.ms",
            "go.mnaspm.com",
            "go.xxxviijmp.com",
            "stripchat.com",
            "www.google-analytics.com",
            "img.strpst.com",
            "video.ktkjmp.com",
            "creative.mnaspm.com",
            "assets.strpst.com",
            "s3t3d2y8.afcdn.net",
            "u.clarity.ms",
            "s.pemsrv.com",
            "pm.w55c.net",
            "ml314.com",
            "match.360yield.com",
            "platfrom-cdn.sharethis.com",
            "sync.sharethis.com",
            "ups.analytics.com",
            "bcp.crwdcntrl.net",
            "ups.analytics.yahoo.com",
            "match.adsrvr.org",
            "ps.eyeota.net",
            "px.ads.linkedin.com",
            "cms.analytics.yahoo.com",
            "t.sharethis.com",
            "l.sharethis.com",
            "count-server.sharethis.com"
        ];

        await patterns
            .Select(pattern => $"https://{pattern}")
            .ToAsyncEnumerable()
            .ForEachAwaitAsync(
                async pattern => {
                    await context.RouteAsync(
                        $"{pattern}/**", async route => await route.AbortAsync());
                });
    }

    private static async Task ScrollToBottom(IPage page) {
        while (true) {
            await page.ScrollTo(await page.GetTotalHeight());
            await WaitImagesLoaded(page);

            if (!await NotAtBottom(page)) { // until we can't scroll down more
                return;
            }
        }
    }

    private static async Task<bool> NotAtBottom(IPage page) {
        var previousHeight = await page.GetVerticalPosition();
        await page.ScrollBy(1);
        var currentHeight = await page.GetVerticalPosition();
        return currentHeight > previousHeight;
    }

    private static async Task WaitImagesLoaded(IPage page) {
        // ReSharper disable once VariableHidesOuterVariable
        async Task<bool> HasImageUnloaded(IPage page) {
            var loadingTextIndicator = page.Locator("div.pagination-loading");
            if (await loadingTextIndicator.IsVisibleAsync()) {
                return true;
            } else {
                var imgs = page.Locator("p.item-image img");
                return await (await imgs.AllAsync())
                    .ToAsyncEnumerable()
                    .SelectAwait(
                        async img =>
                            RequireNonNull(await img.GetAttributeAsync("src"), $"Image {img} has no 'src'")
                    ).AnyAsync(src => src.EndsWith(".gif")); // loading animation
            }
        }

        while (await HasImageUnloaded(page)) {
            if (await NotAtBottom(page)) {
                return; // break the loop to allow caller to scroll down more to load images
            } else {
                await page.WaitForTimeoutAsync(500);
            }
        }
    }

    private static async Task<bool?> AreAllImagesLoaded(IPage page) {
        var title = page.Locator("h1.focusbox-title");
        var text = await title.InnerTextAsync();
        var match = ImgCountPattern().Match(text);
        if (match.Success) {
            var expectedCount = int.Parse(match.Groups[1].Value);
            var imgs = page.Locator("p.item-image img");
            var actualCount = await imgs.CountAsync();
            return actualCount == expectedCount;
        } else {
            return null; // No way to determine if all images are loaded
        }
    }

    private static async Task LoadAllImage(IPage page) {
        Logger.LogDebug("Enter '{LoadAllImages}' loop", nameof(LoadAllImage));
        await Spinner.StartAsync(
            "Loading images", async spinner => {
                var times = 0;
                var startTime = DateTime.Now;
                while (true) {
                    await ScrollToBottom(page);
                    var button = page.Locator("div.ias_trigger");
                    if (await button.IsVisibleAsync()) {
                        await button.ClickAsync();
                        {
                            times += 1;
                            var elapsedTime = DateTime.Now - startTime;
                            spinner.Text =
                                $"Loading more images, {elapsedTime.Humanize()} elapsed (#{times})";
                        }
#if DEBUG
                        if (times == 10) { // skip further images to expedite debugging
                            return;
                        }
#endif
                    } else { // If the "load more" button doesn't exist, all images should have been loaded
                        var all = await AreAllImagesLoaded(page);
                        if (all is not null) {
                            if (all is true) {
                                var totalTime = DateTime.Now - startTime;
                                spinner.Succeed(
                                    $"All images loaded ({totalTime.Humanize()})");
                                return;
                            } else {
                                spinner.Text = "Loading more images (not all images loaded, retrying...)";
                                Logger.LogDebug("Retry loading images");
                            }
                        } else {
                            spinner.Text = "Probably all images loaded";
                            Logger.LogDebug("No way to guarantee all images are loaded");
                            return;
                        }
                    }
                }
            });
    }

    private static async Task BypassPageConfirmation(IPage page) {
        var button = page.Locator("#agree-over18");
        try {
            await button.ClickAsync(new LocatorClickOptions { Timeout = 5_000 });
        } catch (TimeoutException) {}
    }

    private static async Task HideElements(IPage page) {
        ILocator[] locators = [
            page.Locator("div.line_03"),
            page.Locator("div.excerpts-article"),
            page.Locator("footer.footer")
        ];
        await locators.HideAll();
    }

    private static async Task<string[]> ExtractSrcLinks(IPage page, IEnumerable<ILocator> locators) {
        var prefix = $"https://{new Uri(page.Url).Host}";
        return await locators
            .ToAsyncEnumerable()
            .SelectAwait(
                async locator => RequireNonNull(
                    await locator.GetAttributeAsync("src"), $"'{locator}' has no 'src'"))
            .Select(src => $"{prefix}{src}")
            .ToArrayAsync();
    }

    private static async Task DownloadImages(IPage page, string directory) {
        var imgs = await page.Locator("p.item-image img").AllAsync();
        var count = imgs.Count;

        var imgUrls = await ExtractSrcLinks(page, imgs);
        Console.WriteLine($"Already prepared to download {count} images");

        using var bar = new ProgressBar(
            count, "Downloading images", new ProgressBarOptions { ProgressCharacter = '=' });

        CancelConnectionLimit();
        using var client = await page.NewHttpClient();
        await imgUrls.ForEachWithLimitedConcurrency(
            async (url, index) => {
                await client.Download(url, Path.Combine(directory, $"{index}{url.GetExtension()}"), Logger);
                bar.Tick();
            },
            8);
    }

    private static void CancelConnectionLimit() {
        var defaultLimit = ServicePointManager.DefaultConnectionLimit;
        Logger.LogDebug("Default connection limit: {}", defaultLimit);
        if (defaultLimit == 2) {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            Logger.LogDebug("Default connection limit canceled");
        }
    }

    private static Cli? ParseCli(string[] args) {
        RequireNonNull(args);

        Cli? cli = null;
        Parser.Default.ParseArguments<Cli>(args)
            .WithParsed(inner => { cli = inner; });
        return cli;
    }

    private static void VerifyCli(Cli cli) {
        Directory.CreateDirectory(cli.Dir);
    }

    public static async Task Main(string[] args) {
        var cli = ParseCli(args);
        if (cli is not null) { // allow `--help`
            VerifyCli(cli);
            await RunInBrowser(
                cli.Url,
                async (_, page) => {
                    await BypassPageConfirmation(page);
                    await HideElements(page);
                    await LoadAllImage(page);
                    await DownloadImages(page, cli.Dir);
                },
                BlockRequest,
                Logger
            );
        }
    }

    [GeneratedRegex(@"(\d+)P$")]
    private static partial Regex ImgCountPattern();
}
