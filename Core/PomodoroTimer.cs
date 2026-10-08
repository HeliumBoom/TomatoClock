namespace TomatoClock.Core;

public enum TimerPhase { Work, Break }
public enum TimerState { Idle, Running, Paused }

/// <summary>Deterministic timer. The caller supplies active elapsed time, excluding suspension.</summary>
public sealed class PomodoroTimer
{
    public int WorkMinutes { get; private set; }
    public int BreakMinutes { get; private set; }
    public TimerPhase Phase { get; private set; } = TimerPhase.Work;
    public TimerState State { get; private set; } = TimerState.Idle;
    public TimeSpan Duration { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public double Progress => 1 - Remaining.TotalSeconds / Duration.TotalSeconds;
    public string DisplayTime
    {
        get
        {
            long seconds = (long)Math.Ceiling(Remaining.TotalSeconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }

    public PomodoroTimer(int workMinutes = 25, int breakMinutes = 5) => Configure(workMinutes, breakMinutes);

    public void Configure(int workMinutes, int breakMinutes)
    {
        if (workMinutes is < 1 or > 180) throw new ArgumentOutOfRangeException(nameof(workMinutes));
        if (breakMinutes is < 1 or > 180) throw new ArgumentOutOfRangeException(nameof(breakMinutes));
        WorkMinutes = workMinutes;
        BreakMinutes = breakMinutes;
        if (State == TimerState.Idle) Reset();
    }

    public void Start() => State = TimerState.Running;
    public void Pause() { if (State == TimerState.Running) State = TimerState.Paused; }
    public void Reset()
    {
        State = TimerState.Idle;
        Phase = TimerPhase.Work;
        Duration = Remaining = TimeSpan.FromMinutes(WorkMinutes);
    }

    /// <returns>True when at least one phase changed, allowing one coalesced notification.</returns>
    public bool Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (State != TimerState.Running || elapsed == TimeSpan.Zero) return false;
        if (elapsed < Remaining) { Remaining -= elapsed; return false; }

        elapsed -= Remaining;
        SwitchPhase();
        // After the first boundary all durations use the current settings. Skip whole
        // cycles in constant time, including an arbitrarily long delayed UI update.
        long cycleTicks = TimeSpan.FromMinutes(WorkMinutes + BreakMinutes).Ticks;
        elapsed = TimeSpan.FromTicks(elapsed.Ticks % cycleTicks);
        if (elapsed >= Remaining) { elapsed -= Remaining; SwitchPhase(); }
        Remaining -= elapsed;
        return true;
    }

    private void SwitchPhase()
    {
        Phase = Phase == TimerPhase.Work ? TimerPhase.Break : TimerPhase.Work;
        Duration = Remaining = TimeSpan.FromMinutes(Phase == TimerPhase.Work ? WorkMinutes : BreakMinutes);
    }
}
