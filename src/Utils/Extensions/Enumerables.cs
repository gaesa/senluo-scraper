namespace SenluoScraper.Utils.Extensions;

// ReSharper disable once IdentifierTypo
internal static class Enumerables {
    internal static async Task ForEachWithLimitedConcurrency<T>(
        this IEnumerable<T> enumerable, Func<T, int, Task> action, int limit
    ) {
        using var semaphore = new SemaphoreSlim(limit, limit);
        var tasks = enumerable.Select(
            async (ele, i) => {
                // ReSharper disable once AccessToDisposedClosure
                await semaphore.WaitAsync();
                try {
                    await action(ele, i);
                } finally {
                    // ReSharper disable once AccessToDisposedClosure
                    semaphore.Release();
                }
            });
        await Task.WhenAll(tasks);
    }

    internal static async Task ForEachWithLimitedConcurrency<T>(
        this IEnumerable<T> enumerable, Func<T, Task> action, int limit
    ) {
        using var semaphore = new SemaphoreSlim(limit, limit);
        var tasks = enumerable.Select(
            async ele => {
                // ReSharper disable once AccessToDisposedClosure
                await semaphore.WaitAsync();
                try {
                    await action(ele);
                } finally {
                    // ReSharper disable once AccessToDisposedClosure
                    semaphore.Release();
                }
            });
        await Task.WhenAll(tasks);
    }

    internal static IEnumerable<T> Once<T>(this T value) {
        return Enumerable.Repeat(value, 1);
    }
}
