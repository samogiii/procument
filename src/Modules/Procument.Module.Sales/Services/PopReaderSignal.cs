namespace Procument.Module.Sales.Services;

/// <summary>Wakes the POP reader (PopReaderWorker in the API) when a new POP is queued, instead of waiting for its next round.</summary>
public class PopReaderSignal
{
    private readonly SemaphoreSlim _gate = new(0, 1);

    public void Poke()
    {
        try { if (_gate.CurrentCount == 0) _gate.Release(); }
        catch (SemaphoreFullException) { }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _gate.WaitAsync(timeout, ct);
}
