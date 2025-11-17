using Robust.Shared.GameStates;

namespace Content.Shared._Europa.Soulbreakers;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SoulbreakerCollarSystem))]
public sealed partial class SoulbreakerEnslavedComponent : Component
{
}
