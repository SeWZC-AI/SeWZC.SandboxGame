namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>Nation 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class NationCursor : StateCursor<Nation>
{
    private MilitaryRecordCursor? _military;
    public NationCursor(Nation value) : base(value) { }

    public int Id
    {
        get => Value.Id;
    }

    public string Name
    {
        get => Value.Name;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Name, value))
                ReplaceChanged(Value with { Name = value });
        }
    }

    public uint ColorArgb
    {
        get => Value.ColorArgb;
        set
        {
            if (!EqualityComparer<uint>.Default.Equals(Value.ColorArgb, value))
                ReplaceChanged(Value with { ColorArgb = value });
        }
    }

    public RaceKind FoundingRace
    {
        get => Value.FoundingRace;
    }

    public int CapitalId
    {
        get => Value.CapitalId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.CapitalId, value))
                ReplaceChanged(Value with { CapitalId = value });
        }
    }

    public int Population
    {
        get => Value.Population;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Population, value))
                ReplaceChanged(Value with { Population = value });
        }
    }

    public int Technology
    {
        get => Value.Technology;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Technology, value))
                ReplaceChanged(Value with { Technology = value });
        }
    }

    public string Decision
    {
        get => Value.Decision;
        set
        {
            if (!EqualityComparer<string>.Default.Equals(Value.Decision, value))
                ReplaceChanged(Value with { Decision = value });
        }
    }

    public ResourceStock Resources
    {
        get => Value.Resources;
        set
        {
            if (!EqualityComparer<ResourceStock>.Default.Equals(Value.Resources, value))
                ReplaceChanged(Value with { Resources = value });
        }
    }

    public DevelopmentFocus DevelopmentFocus
    {
        get => Value.DevelopmentFocus;
        set
        {
            if (!EqualityComparer<DevelopmentFocus>.Default.Equals(Value.DevelopmentFocus, value))
                ReplaceChanged(Value with { DevelopmentFocus = value });
        }
    }

    public MilitaryRecordCursor Military
    {
        get
        {
            if (_military is null)
            {
                _military = new MilitaryRecordCursor(Value.Military);
                _military.Bind(value =>
                {
                    if (!ReferenceEquals(Value.Military, value))
                        ReplaceChanged(Value with { Military = value });
                });
            }

            return _military;
        }
    }

    public int CultureId
    {
        get => Value.CultureId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.CultureId, value))
                ReplaceChanged(Value with { CultureId = value });
        }
    }

    public int RepresentativeId
    {
        get => Value.RepresentativeId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.RepresentativeId, value))
                ReplaceChanged(Value with { RepresentativeId = value });
        }
    }

    public static implicit operator Nation(NationCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator NationCursor(Nation value)
    {
        return new NationCursor(value);
    }

    protected override void OnReplace(in Nation before, in Nation after)
    {
        if (_military is not null && !ReferenceEquals(_military.Value, after.Military))
            _military.Synchronize(after.Military);
    }
}
