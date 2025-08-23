namespace uTest.Runner.Util;

/// <summary>
/// Extensions for <see cref="CountdownEvent"/>.
/// <para>Stolen from <see href="https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Helpers/CountDownEventExtensions.cs"/>.</para>
/// </summary>
internal static class CountDownEventExtensions
{
    public static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, CancellationToken cancellationToken)
        => await countdownEvent.WaitAsync(uint.MaxValue, cancellationToken).ConfigureAwait(false);

    public static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, TimeSpan timeout, CancellationToken cancellationToken)
        => await countdownEvent.WaitAsync((uint)timeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);

    internal static async Task<bool> WaitAsync(this CountdownEvent countdownEvent, uint millisecondsTimeOutInterval, CancellationToken cancellationToken)
    {
        RegisteredWaitHandle? registeredHandle = null;
        CancellationTokenRegistration tokenRegistration = default;
        try
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            registeredHandle = ThreadPool.RegisterWaitForSingleObject(countdownEvent.WaitHandle,
                (state, timedOut) => ((TaskCompletionSource<bool>)state!).TrySetResult(!timedOut),
                tcs, millisecondsTimeOutInterval, true
            );

            tokenRegistration = cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), tcs);

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            registeredHandle?.Unregister(null);
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            await tokenRegistration.DisposeAsync().ConfigureAwait(false);
#else
            tokenRegistration.Dispose();
#endif
        }
    }
}