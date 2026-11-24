using LmenV2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LmenV2.Tests;

[TestClass]
public sealed class LyricClockReconcilerTests
{
    [TestMethod]
    public void ReconcileMarksBackwardJumpToBeginningAsRestart()
    {
        var clock = new LyricClockReconciler();
        clock.Reset(TimeSpan.FromSeconds(18), isPlaying: true);

        clock.Reconcile(
            TimeSpan.FromSeconds(18),
            TimeSpan.Zero,
            isPlaying: true);

        Assert.IsTrue(clock.LastUpdateWasRestart);
    }

    [TestMethod]
    public void ResetClearsRestartSignal()
    {
        var clock = new LyricClockReconciler();
        clock.Reset(TimeSpan.FromSeconds(18), isPlaying: true);
        clock.Reconcile(TimeSpan.FromSeconds(18), TimeSpan.Zero, isPlaying: true);

        clock.Reset(TimeSpan.FromSeconds(4), isPlaying: true);

        Assert.IsFalse(clock.LastUpdateWasRestart);
    }
}
