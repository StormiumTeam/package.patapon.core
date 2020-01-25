using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.NetCode;

namespace Systems.GamePlay.CYari
{
	[UpdateInGroup(typeof(RhythmAbilitySystemGroup))]
	[UpdateInWorld(UpdateInWorld.TargetWorld.Server)]
	[AlwaysSynchronizeSystem]
	public abstract class BaseAbilitySystem : JobGameBaseSystem
	{
	}
}