namespace SeWZC.WorldBox.Core;

public sealed partial class ImmutableVector<T>
{
    // 每批连续写入只复制一次触及的分支；冻结后交出数组所有权，后续更新重新复制。
    internal sealed class Builder(ImmutableVector<T> snapshot)
    {
        private MutableNode? _leaf;
        private int _leafStart = -1;
        private MutableNode? _root;
        private ImmutableVector<T> _snapshot = snapshot;

        internal void SetItem(int index, T value)
        {
            if ((uint)index >= (uint)_snapshot.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var start = index & ~Mask;
            if (start == _leafStart)
            {
                _leaf!.Values[index & Mask] = value;
                return;
            }

            var node = _root ??= new MutableNode(_snapshot.MergeChange());
            for (var shift = _snapshot._shift; shift > 0; shift -= Bits)
            {
                var slot = (index >> shift) & Mask;
                if (node.Values[slot] is not MutableNode child)
                {
                    child = new MutableNode((object?[])node.Values[slot]!);
                    node.Values[slot] = child;
                }

                node = child;
            }

            _leaf = node;
            _leafStart = start;
            node.Values[index & Mask] = value;
        }

        internal ImmutableVector<T> Freeze()
        {
            if (_root is null)
                return _snapshot;
            _snapshot = new ImmutableVector<T>(_root.Freeze(_snapshot._shift), _snapshot._shift, _snapshot.Count);
            _root = null;
            _leaf = null;
            _leafStart = -1;
            return _snapshot;
        }

        private sealed class MutableNode(object?[] previous)
        {
            internal object?[] Values { get; } = CopyNode(previous);

            internal object?[] Freeze(int shift)
            {
                if (shift > 0)
                {
                    for (var slot = 0; slot < Width; slot++)
                        if (Values[slot] is MutableNode child)
                            Values[slot] = child.Freeze(shift - Bits);
                }

                return Values;
            }
        }
    }
}
