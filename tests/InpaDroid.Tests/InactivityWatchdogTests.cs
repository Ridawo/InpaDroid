using InpaDroid.Diag;

namespace InpaDroid.Tests;

public sealed class InactivityWatchdogTests
{
    [Fact]
    public void Expires_WhenThereIsNoActivity()
    {
        var expired = new ManualResetEventSlim();   // sin using: el temporizador podría llamar a Set tras un fallo
        var watchdog = new InactivityWatchdog(TimeSpan.FromMilliseconds(100), expired.Set);

        Assert.True(expired.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(watchdog.Fired);
        Assert.True(watchdog.Finish());
    }

    [Fact]
    public void Kick_PostponesExpiry_WhileTheJobKeepsProgressing()
    {
        var expired = new ManualResetEventSlim();   // sin using: el temporizador podría llamar a Set tras un fallo
        var watchdog = new InactivityWatchdog(TimeSpan.FromMilliseconds(500), expired.Set);

        // Actividad durante el triple del plazo: un job largo que sigue avanzando no se corta.
        var until = DateTime.UtcNow.AddMilliseconds(1500);
        while (DateTime.UtcNow < until)
        {
            watchdog.Kick();
            Thread.Sleep(20);
        }
        Assert.False(watchdog.Fired);

        // Cuando deja de haber actividad, vence.
        Assert.True(expired.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(watchdog.Finish());
    }

    [Fact]
    public void Finish_BeforeLimit_NeverExpires()
    {
        int calls = 0;
        var watchdog = new InactivityWatchdog(TimeSpan.FromMilliseconds(100), () => Interlocked.Increment(ref calls));

        Assert.False(watchdog.Finish());
        Thread.Sleep(300);
        Assert.Equal(0, Volatile.Read(ref calls));
        Assert.False(watchdog.Fired);
    }
}
