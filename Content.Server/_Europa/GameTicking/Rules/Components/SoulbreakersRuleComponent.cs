using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization;

namespace Content.Server._Europa.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(SoulbreakersRuleSystem))]
public sealed partial class SoulbreakersRuleComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextLogicTick;

    [DataField]
    public TimeSpan EndCheckDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public bool RoundstartDelayEnded = false;

    [DataField]
    public TimeSpan RoundstartDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public int EnslavedCount = 0;

    [DataField]
    public float EnslavedStonks = 0;

    [DataField]
    public List<SoldSlaveInfo> SoldSlaves = new();

    [ViewVariables]
    public bool PlayedIslamicTrance = false;

    [ViewVariables]
    public bool PlayedSoulbreakersWin = false;
}

[Serializable, NetSerializable]
public sealed class SoldSlaveInfo
{
    public string Name = string.Empty;
    public float Price;
}
