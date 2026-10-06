namespace SeWZC.WorldBox.Core;

/// <summary>局部冲突影响到的参与者范围。</summary>
public enum ConflictScope
{
    /// <summary>个人之间。</summary>
    Individual,

    /// <summary>群体之间。</summary>
    Group,

    /// <summary>聚落层面。</summary>
    Settlement,
}
