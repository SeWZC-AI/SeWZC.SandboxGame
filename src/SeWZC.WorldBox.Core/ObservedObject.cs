namespace SeWZC.WorldBox.Core;

/// <summary>以类别和稳定 ID 标识的关注对象。</summary>
/// <param name="Kind">关注对象的类别。</param>
/// <param name="Id">该类别下对象的稳定 ID。</param>
public readonly record struct ObservedObject(ObservedObjectKind Kind, int Id);
