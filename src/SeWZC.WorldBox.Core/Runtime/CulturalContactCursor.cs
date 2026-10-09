namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>CulturalContact 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class CulturalContactCursor(CulturalContact value) : StateCursor<CulturalContact>(value)
{

    public int ResidentId
    {
        get => Value.ResidentId;
    }

    public int CultureId
    {
        get => Value.CultureId;
    }

    public double Exposure
    {
        get => Value.Exposure;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Exposure, value))
                ReplaceChanged(Value with { Exposure = value });
        }
    }

    public long LastContactTick
    {
        get => Value.LastContactTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastContactTick, value))
                ReplaceChanged(Value with { LastContactTick = value });
        }
    }

    public static implicit operator CulturalContact(CulturalContactCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator CulturalContactCursor(CulturalContact value)
    {
        return new CulturalContactCursor(value);
    }
}
