namespace TimerManager;

public enum TimerState { Stopped, Running, Paused, Finished }

public class TimerEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public TimeSpan CountdownDuration { get; set; }
    public string? SoundPath { get; set; }
    /// <summary>Optionale Kategorie-/Akzentfarbe als ARGB-Wert; null = zustandsabhängige Standardfarbe.</summary>
    public int? AccentColorArgb { get; set; }

    private TimeSpan _elapsed = TimeSpan.Zero;
    private DateTime _startedAt;
    private DateTime _finishedAt;
    private TimerState _state = TimerState.Stopped;

    public TimerState State => _state;

    // ── Aktions-Historie (letzte 5, nur im Speicher) ──────────────────
    private readonly List<(DateTime Time, string Text)> _log = [];
    private bool _adjustGroupActive;   // true, solange der jüngste Eintrag eine Minuten-Anpassung ist
    private int _adjustGroupMinutes;   // aufsummierte Minuten der laufenden Anpassungs-Gruppe
    private DateTime _lastAdjustTime;  // Zeitpunkt der letzten Minuten-Anpassung

    /// <summary>Die letzten (max. 5) Aktionen mit Zeitstempel, älteste zuerst.</summary>
    public IReadOnlyList<(DateTime Time, string Text)> ActionLog => _log;

    private void Log(string action)
    {
        _log.Add((DateTime.Now, action));
        if (_log.Count > 5) _log.RemoveAt(0);
        _adjustGroupActive = false;  // jede andere Aktion beendet eine Anpassungs-Gruppe
    }

    /// <summary>
    /// Protokolliert eine Minuten-Anpassung. Aufeinanderfolgende Anpassungen werden zu
    /// einem Eintrag zusammengefasst, solange jede höchstens 3 s nach der vorherigen erfolgt.
    /// </summary>
    private void LogAdjustment(int minutes)
    {
        var now = DateTime.Now;
        if (_adjustGroupActive && _log.Count > 0 && now - _lastAdjustTime <= TimeSpan.FromSeconds(3))
        {
            _adjustGroupMinutes += minutes;
            _lastAdjustTime = now;
            _log[^1] = (now, FormatAdjust(_adjustGroupMinutes));  // Summe im selben Eintrag aktualisieren
        }
        else
        {
            _adjustGroupMinutes = minutes;
            _lastAdjustTime = now;
            Log(FormatAdjust(minutes));   // setzt _adjustGroupActive = false …
            _adjustGroupActive = true;    // … und startet hier eine neue Gruppe
        }
    }

    private static string FormatAdjust(int minutes) =>
        minutes > 0 ? $"+{minutes} Min" : minutes < 0 ? $"-{Math.Abs(minutes)} Min" : "±0 Min";

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
        Log("Gestartet");
    }

    public void Pause()
    {
        if (_state is not TimerState.Running) return;
        _elapsed += DateTime.Now - _startedAt;
        _state = TimerState.Paused;
        Log("Pausiert");
    }

    public void Reset()
    {
        _elapsed = TimeSpan.Zero;
        _finishedAt = default;
        _state = TimerState.Stopped;
        Log("Zurückgesetzt");
    }

    /// <summary>
    /// Verschiebt die verbleibende Zeit eines laufenden oder pausierten Timers um
    /// <paramref name="delta"/> (positiv = mehr Restzeit, negativ = weniger), ohne die
    /// gespeicherte <see cref="CountdownDuration"/> zu verändern. Ein Reset stellt daher
    /// wieder die volle Dauer her. Bei gestoppten/abgelaufenen Timern wirkungslos.
    /// </summary>
    public void AdjustRemaining(TimeSpan delta)
    {
        if (_state is not (TimerState.Running or TimerState.Paused)) return;
        // Restzeit = CountdownDuration - Elapsed  →  mehr Restzeit bedeutet weniger Elapsed
        _elapsed -= delta;
        LogAdjustment((int)Math.Round(delta.TotalMinutes));
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
                Log("Abgelaufen");
            }
            return TimeSpan.Zero;
        }
        return remaining;
    }
}
