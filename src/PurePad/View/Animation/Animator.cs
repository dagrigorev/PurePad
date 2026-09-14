using System.Windows.Forms;

namespace PurePad.View.Animation;

/// <summary>
/// A tiny tweening helper built on the WinForms UI timer. It drives a normalised progress
/// value from 0 to 1 through an easing curve and hands each step to a callback, so callers
/// animate any property (opacity, height, width) without owning timer plumbing.
/// </summary>
public static class Animator
{
    private const int FrameIntervalMs = 15; // ~66 fps

    /// <summary>Smooth ease-in-out (cubic) for natural acceleration and deceleration.</summary>
    public static double EaseInOut(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    /// <summary>
    /// Animate <paramref name="step"/> from 0..1 over <paramref name="durationMs"/>.
    /// <paramref name="step"/> receives the eased value; <paramref name="completed"/> runs once at the end.
    /// </summary>
    public static void Animate(int durationMs, Action<double> step, Action? completed = null, Func<double, double>? easing = null)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (durationMs <= 0)
        {
            step(1.0);
            completed?.Invoke();
            return;
        }

        easing ??= EaseInOut;
        var start = DateTime.UtcNow;
        var timer = new System.Windows.Forms.Timer { Interval = FrameIntervalMs };

        timer.Tick += (s, e) =>
        {
            double elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            double progress = Math.Min(1.0, elapsed / durationMs);
            step(easing(progress));

            if (progress >= 1.0)
            {
                timer.Stop();
                timer.Dispose();
                completed?.Invoke();
            }
        };

        timer.Start();
    }

    /// <summary>Animate an integer property (e.g. a panel's Height) between two values.</summary>
    public static void AnimateInt(int from, int to, int durationMs, Action<int> apply, Action? completed = null)
    {
        Animate(durationMs, t => apply((int)Math.Round(from + (to - from) * t)), completed);
    }
}
