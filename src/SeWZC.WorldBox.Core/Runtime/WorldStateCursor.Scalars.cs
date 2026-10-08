namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class WorldStateCursor
{
    private uint _randomState;
    private int _scalarDepth, _nextId;
    private bool _scalarsChanged;
    private long _tick;

    internal ScalarScope BeginScalarUpdates()
    {
        if (_scalarDepth++ == 0)
        {
            _tick = Value.Tick;
            _randomState = Value.RandomState;
            _nextId = Value.NextId;
        }

        return new ScalarScope(this);
    }

    private void FlushScalars()
    {
        if (!_scalarsChanged)
            return;
        ReplaceChanged(Value with { Tick = _tick, RandomState = _randomState, NextId = _nextId });
        _scalarsChanged = false;
    }

    internal readonly struct ScalarScope(WorldStateCursor owner) : IDisposable
    {
        public void Dispose()
        {
            if (--owner._scalarDepth == 0)
                owner.FlushScalars();
        }
    }
}
