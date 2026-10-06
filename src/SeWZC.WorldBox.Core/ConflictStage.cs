namespace SeWZC.WorldBox.Core;

/// <summary>局部资源冲突的升级或解决阶段。</summary>
public enum ConflictStage
{
    /// <summary>争执。</summary>
    Dispute,
    /// <summary>对峙。</summary>
    Confrontation,
    /// <summary>暴力冲突。</summary>
    Violence,
    /// <summary>已经解决。</summary>
    Resolved,
}
