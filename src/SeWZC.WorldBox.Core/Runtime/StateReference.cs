namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>引擎内的稳定定位引用；当前值不可变，转换后的状态通过 Replace 显式提交。</summary>
internal sealed class StateReference<T>(T value) where T : class
{
    internal ICollectionOwner? Collection { get; set; }
    internal int Position { get; set; }
    public T Value { get; private set; } = value;

    internal void Synchronize(T value)
    {
        Value = value;
    }

    public void Replace(T value)
    {
        if (ReferenceEquals(Value, value))
            return;
        if (Collection is { } collection)
            collection.Replace(this, value);
        else
            Value = value;
    }

    internal interface ICollectionOwner
    {
        void Replace(StateReference<T> reference, T value);
    }
}
