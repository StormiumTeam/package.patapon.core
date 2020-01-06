using package.patapon.core.FeverWorm;
using StormiumTeam.GameBase.Systems;
using Unity.Entities;

namespace Patapon.Client.OrderSystems
{
	[UpdateInGroup(typeof(OrderInterfaceSystemGroup))]
	[UpdateAfter(typeof(FeverWormOrderingSystem))]
	public class RhythmEngineBeatFrameOrdering : OrderingSystem
	{
	}
}