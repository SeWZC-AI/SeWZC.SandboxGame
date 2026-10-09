namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>引擎内的稳定定位引用；只读取当前值，转换后的状态通过 Replace 显式提交。</summary>
internal class StateReference<T>(T value)
{
    private T _value = value;
    private ICollectionOwner? _collection;
    internal ICollectionOwner? Collection
    {
        get => _collection;
        set
        {
            _collection = value;
            PendingCommit = false;
        }
    }
    internal int Position { get; set; }
    internal bool PendingCommit { get; set; }
    public ref readonly T Value => ref _value;

    internal virtual void FlushPending() { }

    internal void Synchronize(in T value)
    {
        OnReplace(_value, value);
        _value = value;
    }

    protected virtual void OnReplace(in T before, in T after) { }

    public virtual void Replace(in T value)
    {
        if (typeof(T).IsValueType ? EqualityComparer<T>.Default.Equals(_value, value) : ReferenceEquals(_value, value))
            return;
        ReplaceChanged(value);
    }

    protected void ReplaceChanged(in T value)
    {
        if (Collection is { } collection)
            collection.Replace(this, value);
        else
            Synchronize(value);
    }

    internal interface ICollectionOwner
    {
        void Replace(StateReference<T> reference, in T value);
        void RegisterPending(StateReference<T> reference);
    }
}
