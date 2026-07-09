namespace TimerManager;

public enum TimerState { Stopped, Running, Paused, Finished }

public class TimerEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public TimeSpan CountdownDuration { get; set; }
    public string? SoundPath { get; set; }

    private TimeSpan _elapsed = TimeSpan.Zero;
    private DateTime _startedAt;
    private DateTime _finishedAt;
    private TimerState _state = TimerState.Stopped;

    public TimerState State => _state;

    // ── Persistenz: Rohzustand auslesen/wiederherstellen ──────────────
    /// <summary>Verstrichene Zeit ohne das aktuell laufende Segment (Rohfeld).</summary>
    public TimeSpan ElapsedRaw => _elapsed;
    /// <summary>Absoluter Zeitpunkt, an dem das aktuelle Lauf-Segment gestartet wurde.</summary>
    public DateTime StartedAt => _startedAt;
    /// <summary>Absoluter Zeitpunkt, an dem der Timer abgelaufen ist.</summary>
    public DateTime FinishedAt => _finishedAt;

    /// <summary>
    /// Stellt einen gespeicherten Laufzeit-Zustand wieder her. Für einen laufenden
    /// Timer wird <paramref name="startedAt"/> als absoluter Zeitpunkt übernommen, sodass
    /// die Zeit auch während geschlossener App weiterläuft (Wanduhr-Verhalten).
    /// </summary>
    public void Restore(TimerState state, TimeSpan elapsed, DateTime startedAt, DateTime finishedAt)
    {
        _state = state;
        _elapsed = elapsed;
        _startedAt = startedAt;
        _finishedAt = finishedAt;
    }

    public TimerEntry(string name, TimeSpan countdownDuration = default)
    {
        Name = name;
        CountdownDuration = countdownDuration;
    }

    public void Start()
    {
        if (_state is TimerState.Running) return;
        _startedAt = DateTime.Now;
        _state = TimerState.Running;
    }

    public void Pause()
    {
        if (_state is not TimerState.Running) return;
        _elapsed += DateTime.Now - _startedAt;
        _state = TimerState.Paused;
    }

    public void Reset()
    {
        _elapsed = TimeSpan.Zero;
        _finishedAt = default;
        _state = TimerState.Stopped;
    }

    /// <summary>Gibt zurück, wie lange der Timer bereits abgelaufen ist (nur im Zustand Finished).</summary>
    public TimeSpan GetOvertime()
        => _state is TimerState.Finished ? DateTime.Now - _finishedAt : TimeSpan.Zero;

    public TimeSpan GetElapsed()
    {
        if (_state is TimerState.Running)
            return _elapsed + (DateTime.Now - _startedAt);
        return _elapsed;
    }

    public TimeSpan GetDisplay()
    {
        var remaining = CountdownDuration - GetElapsed();
        if (remaining <= TimeSpan.Zero)
        {
            if (_state is TimerState.Running)
            {
                _elapsed += DateTime.Now - _startedAt;
                _finishedAt = DateTime.Now;
                _state = TimerState.Finished;
            }
            return TimeSpan.Zero;
        }
        return remaining;
    }
}
