using System;
using System.Windows.Media;

namespace LmenV2.Rendering
{
    public static class RenderFrameClock
    {
        private const double DefaultDeltaSeconds = 1.0 / 60.0;
        private const long TargetFramesPerSecond = 60;

        public static bool TryGetDeltaTime(EventArgs? args, ref TimeSpan lastRenderingTime, out double deltaSeconds)
        {
            if (args is not RenderingEventArgs renderingArgs)
            {
                deltaSeconds = DefaultDeltaSeconds;
                return true;
            }

            TimeSpan renderingTime = renderingArgs.RenderingTime;
            if (lastRenderingTime == TimeSpan.MinValue)
            {
                lastRenderingTime = renderingTime;
                deltaSeconds = DefaultDeltaSeconds;
                return true;
            }

            long frameSlot = renderingTime.Ticks * TargetFramesPerSecond / TimeSpan.TicksPerSecond;
            long previousFrameSlot = lastRenderingTime.Ticks * TargetFramesPerSecond / TimeSpan.TicksPerSecond;
            if (frameSlot <= previousFrameSlot)
            {
                deltaSeconds = 0.0;
                return false;
            }

            double elapsed = (renderingTime - lastRenderingTime).TotalSeconds;
            if (elapsed <= 0.0)
            {
                deltaSeconds = 0.0;
                return false;
            }

            lastRenderingTime = renderingTime;
            deltaSeconds = elapsed > 0.1 ? DefaultDeltaSeconds : elapsed;
            return true;
        }
    }
}
