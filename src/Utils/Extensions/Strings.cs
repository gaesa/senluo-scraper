namespace SenluoScraper.Utils.Extensions;

internal static class StringExtensions {
    internal static string RemovePrefix(this string s, string prefix) {
        return s.StartsWith(prefix) ? s.Remove(0, prefix.Length) : s;
    }
}
