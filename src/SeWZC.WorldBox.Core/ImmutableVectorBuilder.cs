namespace SeWZC.WorldBox.Core;

/// <summary>将集合表达式中的不可变对象复制到持久化序列。</summary>
public static class ImmutableVectorBuilder
{
    /// <summary>建立不共享输入缓冲的持久化序列。</summary>
    /// <typeparam name="T">不可变对象类型。</typeparam>
    /// <param name="items">集合表达式中的对象。</param>
    public static ImmutableVector<T> Create<T>(ReadOnlySpan<T> items) where T : class
    {
        return ImmutableVector<T>.Create(items);
    }
}
