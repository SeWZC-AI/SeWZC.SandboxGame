namespace SeWZC.WorldBox.Core;

public sealed partial record Resident
{
    internal Resident WithTravelMode(TravelMode value) => TravelMode == value ? this : this with { TravelMode = value };

    internal Resident WithActivity(ResidentActivity value)
    {
        if (Health > 0 && ResidentNeedsRules.IsUnconscious(this))
            value = ResidentActivity.Unconscious;
        return Activity == value ? this : this with { Activity = value };
    }

    internal Resident WithHealth(double value) => Health.Equals(value) ? this : this with { Health = value };

    internal Resident WithHunger(double value) => Hunger.Equals(value) ? this : this with { Hunger = value };

    internal Resident WithThirst(double value) => Thirst.Equals(value) ? this : this with { Thirst = value };

    internal Resident WithSicknessTicks(int value) => SicknessTicks == value ? this : this with { SicknessTicks = value };

    internal Resident WithMana(double value) => Mana.Equals(value) ? this : this with { Mana = value };

    internal Resident WithAgent(AgentState value)
    {
        var activity = Health > 0 && ResidentNeedsRules.IsUnconscious(value, Activity) ? ResidentActivity.Unconscious : Activity;
        return ReferenceEquals(Agent, value) && Activity == activity ? this : this with { Agent = value, Activity = activity };
    }

    internal Resident WithInventory(ResourceStock value) => Inventory == value ? this : this with { Inventory = value };

    // 认知、库存与活动共同构成一次动作，源居民及其身体、移动和身份值保持不变。
    internal Resident WithAction(AgentState agent, ResidentActivity activity)
    {
        if (Health > 0 && ResidentNeedsRules.IsUnconscious(agent, Activity))
            activity = ResidentActivity.Unconscious;
        if (ReferenceEquals(Agent, agent) && Activity == activity)
            return this;
        return this with { Agent = agent, Activity = activity };
    }

    internal Resident WithAction(in ResourceStock inventory, AgentState agent, ResidentActivity activity)
    {
        if (Health > 0 && ResidentNeedsRules.IsUnconscious(agent, Activity))
            activity = ResidentActivity.Unconscious;
        if (Inventory == inventory && ReferenceEquals(Agent, agent) && Activity == activity)
            return this;
        return this with
        {
            Inventory = inventory, Agent = agent,
            Activity = activity,
        };
    }

    internal Resident WithSupplies(in ResourceStock inventory, AgentState agent, TravelMode travelMode)
    {
        if (Inventory == inventory && ReferenceEquals(Agent, agent) && TravelMode == travelMode)
            return this;
        if (TravelMode == travelMode)
            return this with { Inventory = inventory, Agent = agent };
        return this with { Inventory = inventory, Agent = agent, TravelMode = travelMode };
    }

    internal Resident WithPosition(int x, int y)
    {
        if (X == x && FromX == x && Y == y && FromY == y)
            return this;
        return this with { MovementState = _movement with { X = x, FromX = x, Y = y, FromY = y } };
    }

    internal Resident BeginMove(int x, int y, long tick, int duration, ResidentActivity activity, AgentState agent)
    {
        if (Health > 0 && ResidentNeedsRules.IsUnconscious(agent, Activity))
            activity = ResidentActivity.Unconscious;
        return this with
        {
            MovementState = new Movement
            {
                FromX = X, FromY = Y, X = x, Y = y, MoveStartedTick = tick,
                MoveDurationTicks = duration, TravelMode = TravelMode,
            },
            Activity = activity,
            Agent = agent,
        };
    }
}
