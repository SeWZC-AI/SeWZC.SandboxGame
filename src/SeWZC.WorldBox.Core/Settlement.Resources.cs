namespace SeWZC.WorldBox.Core;

public sealed partial record Settlement
{
    internal Settlement WithResources(in ResourceStock resources)
    {
        return Resources == resources ? this : this with { Resources = resources };
    }
}
