using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Nation 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class NationCursor : StateCursor<global::SeWZC.WorldBox.Core.Nation>
{
    public NationCursor() : this(new()) { }
    public NationCursor(global::SeWZC.WorldBox.Core.Nation value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.Nation(NationCursor cursor) => cursor.Value;
    public static implicit operator NationCursor(global::SeWZC.WorldBox.Core.Nation value) => new(value);
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) Replace(Value with { Id = value }); } }
    public string Name { get => Value.Name; set { if (!EqualityComparer<string>.Default.Equals(Value.Name, value)) Replace(Value with { Name = value }); } }
    public uint ColorArgb { get => Value.ColorArgb; set { if (!EqualityComparer<uint>.Default.Equals(Value.ColorArgb, value)) Replace(Value with { ColorArgb = value }); } }
    public RaceKind FoundingRace { get => Value.FoundingRace; set { if (!EqualityComparer<RaceKind>.Default.Equals(Value.FoundingRace, value)) Replace(Value with { FoundingRace = value }); } }
    public int CapitalId { get => Value.CapitalId; set { if (!EqualityComparer<int>.Default.Equals(Value.CapitalId, value)) Replace(Value with { CapitalId = value }); } }
    public int Population { get => Value.Population; set { if (!EqualityComparer<int>.Default.Equals(Value.Population, value)) Replace(Value with { Population = value }); } }
    public int Territory { get => Value.Territory; set { if (!EqualityComparer<int>.Default.Equals(Value.Territory, value)) Replace(Value with { Territory = value }); } }
    public int Technology { get => Value.Technology; set { if (!EqualityComparer<int>.Default.Equals(Value.Technology, value)) Replace(Value with { Technology = value }); } }
    public string Decision { get => Value.Decision; set { if (!EqualityComparer<string>.Default.Equals(Value.Decision, value)) Replace(Value with { Decision = value }); } }
    public ResourceStock Resources { get => Value.Resources; set { if (!EqualityComparer<ResourceStock>.Default.Equals(Value.Resources, value)) Replace(Value with { Resources = value }); } }
    public DevelopmentFocus DevelopmentFocus { get => Value.DevelopmentFocus; set { if (!EqualityComparer<DevelopmentFocus>.Default.Equals(Value.DevelopmentFocus, value)) Replace(Value with { DevelopmentFocus = value }); } }
    private MilitaryRecordCursor? _Military;
    public MilitaryRecordCursor Military
    {
        get
        {
            if (_Military is null)
            {
                _Military = new(Value.Military);
                _Military.Bind(value => Replace(Value with { Military = value }));
            }
            return _Military;
        }
        set { _Military = null; Replace(Value with { Military = value.Value }); }
    }
    public int CultureId { get => Value.CultureId; set { if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value)) Replace(Value with { CultureId = value }); } }
    public int RepresentativeId { get => Value.RepresentativeId; set { if (!EqualityComparer<int>.Default.Equals(Value.RepresentativeId, value)) Replace(Value with { RepresentativeId = value }); } }
    protected override void OnReplace(global::SeWZC.WorldBox.Core.Nation before, global::SeWZC.WorldBox.Core.Nation after)
    {
        if (_Military is not null && !ReferenceEquals(_Military.Value, after.Military))
            _Military.Synchronize(after.Military);
    }
}
