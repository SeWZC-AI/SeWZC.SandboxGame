namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>引擎内的状态定位引用；持有不可变值，写入通过替换值并立即通知所属集合完成。</summary>
internal abstract class StateCursor<T>(T value)
{
    private Action<T>? _publish;
    private T _value = value;
    public ref readonly T Value => ref _value;

    public void Bind(Action<T> publish) => _publish = publish;
    internal void Synchronize(T value)
    {
        var previous = _value;
        _value = value;
        OnReplace(previous, value);
    }
    protected virtual void OnReplace(T before, T after) { }

    public void Replace(T value)
    {
        if (EqualityComparer<T>.Default.Equals(_value, value)) return;
        ReplaceChanged(value);
    }

    // 字段设置器已比较目标字段；子状态发布已确认新引用，无需再遍历整个实体判断相等。
    protected void ReplaceChanged(T value)
    {
        Synchronize(value);
        _publish?.Invoke(value);
    }
}
