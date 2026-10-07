namespace SeWZC.WorldBox.Core;

/// <summary>编年史中被关注对象的标识。</summary>
/// <param name="Kind">关注对象的类别。</param>
/// <param name="Id">该类别下对象的稳定 ID。</param>
public readonly record struct ObservedObject(ObservedObjectKind Kind, int Id);
