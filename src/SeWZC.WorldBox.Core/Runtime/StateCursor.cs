namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>引擎内的状态定位引用；持有不可变值，写入通过替换值并立即通知所属集合完成。</summary>
internal abstract class StateCursor<T>(T value)
{
    private Action<T>? _publish;
    private T _value = value;
    internal object? Collection { get; set; }
    internal int Position { get; set; }
    public ref readonly T Value => ref _value;

    internal virtual void FlushPending() { }

    public void Bind(Action<T> publish)
    {
        _publish = publish;
    }

    internal void Synchronize(in T value)
    {
        // 替换前同步派生缓存，直接引用旧值；世界记录较宽，不为通知另复制一份。
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

    // 字段设置器已比较目标字段；子状态发布已确认新引用，无需再遍历整个实体判断相等。
    protected void ReplaceChanged(in T value)
    {
        Synchronize(value);
        _publish?.Invoke(value);
    }
}
