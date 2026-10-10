namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>引擎内的稳定定位引用；当前值不可变，转换后的状态通过 Replace 显式提交。</summary>
internal sealed class StateReference<T>(T value) where T : class
{
    private T _value = value;
    internal ICollectionOwner? Collection { get; set; }
    internal int Position { get; set; }
    public T Value => _value;

    internal void Synchronize(T value) => _value = value;

    public void Replace(T value)
    {
        if (ReferenceEquals(_value, value))
            return;
        if (Collection is { } collection)
            collection.Replace(this, value);
        else
            _value = value;
    }

    internal interface ICollectionOwner
    {
        void Replace(StateReference<T> reference, T value);
    }
}
