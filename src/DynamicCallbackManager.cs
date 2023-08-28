using System.Diagnostics;

namespace MKUtils;

public class DynamicCallbackManager<T> where T : IProgressFactor
{
    public TimeSpan? CooldownBetweenUpdates { get; protected set; }
    public int? FixedUpdateCount { get; protected set; }
    public Action<T>? OnProgress { get; protected set; }
    public Action? OnIdle { get; protected set; }
    public Action<Exception>? OnError { get; protected set; }
    public bool ForceFirstUpdate { get; set; } = false;
    public bool ForceSingleCompleteCall { get; set; } = true;

    private double lastProgressUpdate = 0;
    private Stopwatch stopwatch = new Stopwatch();
    private bool seenFirstUpdate = false;
    private bool seenFirstCompletion = false;

    public DynamicCallbackManager(TimeSpan cooldownBetweenUpdates, Action<T>? OnProgress = null, Action? OnIdle = null, Action<Exception>? OnError = null) : this(OnProgress, OnIdle, OnError)
    {
        this.CooldownBetweenUpdates = cooldownBetweenUpdates;
    }

    public DynamicCallbackManager(int fixedUpdateCount, Action<T>? OnProgress = null, Action? OnIdle = null, Action<Exception>? OnError = null) : this(OnProgress, OnIdle, OnError)
    {
        this.FixedUpdateCount = fixedUpdateCount;
    }

    public DynamicCallbackManager(Action<T>? OnProgress = null, Action? OnIdle = null, Action<Exception>? OnError = null)
    {
        this.OnProgress = OnProgress;
        this.OnIdle = OnIdle;
        this.OnError = OnError;
    }

    public void Idle()
    {
        if (stopwatch.IsRunning) return;
        OnIdle?.Invoke();
    }

    public void Start()
    {
        if (stopwatch.IsRunning) stopwatch.Reset();
        stopwatch.Start();
    }

    public void Update(T progress)
    {
        if (progress.Factor == 1)
        {
            if (ForceSingleCompleteCall)
            {
                if (!seenFirstCompletion)
                {
                    this.OnProgress?.Invoke(progress);
                    seenFirstCompletion = true;
                }
                return;
            }
        }
        if (CooldownBetweenUpdates.HasValue)
        {
            if (stopwatch.ElapsedMilliseconds >= CooldownBetweenUpdates.Value.Milliseconds)
            {
                stopwatch.Restart();
                this.OnProgress?.Invoke(progress);
            }
        }
        else if (FixedUpdateCount.HasValue)
        {
            double delta = progress.Factor - lastProgressUpdate;
            if (!seenFirstUpdate && ForceFirstUpdate || delta > 1f / FixedUpdateCount.Value)
            {
                lastProgressUpdate = progress.Factor;
                this.OnProgress?.Invoke(progress);
            }
        }
        else this.OnProgress?.Invoke(progress);
        seenFirstUpdate = true;
    }

    public void Stop()
    {
        stopwatch.Stop();
    }
}

public interface IProgressFactor
{
    public double Factor { get; }
}