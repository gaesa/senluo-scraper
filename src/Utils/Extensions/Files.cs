using System.Text.Json;
using static SenluoScraper.Utils.Objects;

namespace SenluoScraper.Utils.Extensions;

internal static class FileExtensions {
    internal static async Task<T> ReadJson<T>(this string file) {
        await using var stream = File.OpenRead(file);
        return RequireNonNull(await JsonSerializer.DeserializeAsync<T>(stream), $"Fail to read json file: {file}");
    }

    internal static string GetExtension(this string file) {
        var lastDotIndex = file.LastIndexOf('.');
        return lastDotIndex == 0 ? "" : file[lastDotIndex..];
    }
}
