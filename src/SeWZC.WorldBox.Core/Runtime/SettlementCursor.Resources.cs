namespace SeWZC.WorldBox.Core.Runtime;

internal sealed partial class SettlementCursor
{
    private int _resourceDepth;
    private bool _resourcesChanged;
    private ResourceStock _resourceDraft;

    internal void BeginResourceUpdates()
    {
        if (_resourceDepth++ == 0) _resourceDraft = Value.Resources;
    }

    internal void EndResourceUpdates()
    {
        if (--_resourceDepth == 0) FlushResources();
    }

    internal void FlushResources()
    {
        if (!_resourcesChanged) return;
        ReplaceChanged(Value with { Resources = _resourceDraft });
        _resourcesChanged = false;
    }
}
