using Nito.AsyncEx;

namespace SenluoScraper.Utils.Extensions;

// ReSharper disable once IdentifierTypo
internal static class Enumerables {
    internal static async Task<bool> AnyAsync<T>(this IEnumerable<Task<T>> source, Func<T, bool> predicate) {
        var ordered = source.OrderByCompletion();
        foreach (var task in ordered) {
            var result = await task.ConfigureAwait(false);
            if (predicate(result)) {
                return true;
            }
        }

        return false;
    }

    internal static IEnumerable<Task> FilterForEachAsync<T>(
        this IEnumerable<T> source, Func<T, Task<bool>> predicate, Func<T, Task> action
    ) {
        return source.Select(
            async task => {
                if (await predicate(task)) {
                    await action(task);
                }
            });
    }

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
