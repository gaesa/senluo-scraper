namespace SenluoScraper.Utils;

internal static class Objects {
    internal static T RequireNonNull<T>(T? obj, string message) {
        if (obj is null) {
            throw new NullReferenceException(message);
        } else {
            return obj;
        }
    }

    internal static T RequireNonNull<T>(T? obj) {
        if (obj is null) {
            throw new NullReferenceException();
        } else {
            return obj;
        }
    }
}
