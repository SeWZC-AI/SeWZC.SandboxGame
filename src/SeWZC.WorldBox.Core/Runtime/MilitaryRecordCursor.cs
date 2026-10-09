namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>MilitaryRecord 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class MilitaryRecordCursor : StateCursor<MilitaryRecord>
{
    public MilitaryRecordCursor(MilitaryRecord value) : base(value) { }

    public int CampaignEventId
    {
        get => Value.CampaignEventId;
    }

    public int EnemyNationId
    {
        get => Value.EnemyNationId;
    }

    public int LastMobilizedOrderId
    {
        get => Value.LastMobilizedOrderId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.LastMobilizedOrderId, value))
                ReplaceChanged(Value with { LastMobilizedOrderId = value });
        }
    }

    public long RecoveryUntilTick
    {
        get => Value.RecoveryUntilTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.RecoveryUntilTick, value))
                ReplaceChanged(Value with { RecoveryUntilTick = value });
        }
    }

    public int LastReportEventId
    {
        get => Value.LastReportEventId;
    }

    public long LastReportObservedTick
    {
        get => Value.LastReportObservedTick;
    }

    public static implicit operator MilitaryRecord(MilitaryRecordCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator MilitaryRecordCursor(MilitaryRecord value)
    {
        return new MilitaryRecordCursor(value);
    }
}
