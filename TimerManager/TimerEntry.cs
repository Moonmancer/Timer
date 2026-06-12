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
    private TimerState _state = TimerState.Stopped;

    public TimerState State => _state;

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
        _state = TimerState.Stopped;
    }

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
                _state = TimerState.Finished;
            }
            return TimeSpan.Zero;
        }
        return remaining;
    }
}
