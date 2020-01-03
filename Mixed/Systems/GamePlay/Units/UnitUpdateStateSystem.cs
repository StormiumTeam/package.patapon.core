using Patapon.Mixed.GamePlay.RhythmEngine;
using Patapon.Mixed.RhythmEngine;
using Patapon.Mixed.Units;
using StormiumTeam.GameBase;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Patapon.Mixed.GamePlay
{
	[UpdateInGroup(typeof(UnitInitStateSystemGroup))]
	public class UnitCalculatePlayStateSystem : JobComponentSystem
	{
		private struct JobUpdate : IJobForEachWithEntity<UnitStatistics, UnitPlayState>
		{
			[ReadOnly] public ComponentDataFromEntity<Relative<RhythmEngineDescription>> RhythmEngineRelativeFromEntity;
			[ReadOnly] public ComponentDataFromEntity<GameComboState>                    ComboStateFromEntity;

			public void Execute(Entity entity, int index, [ReadOnly] ref UnitStatistics settings, ref UnitPlayState state)
			{
				GameComboState comboState = default;

				var hasRhythmEngine = RhythmEngineRelativeFromEntity.Exists(entity);
				if (hasRhythmEngine)
				{
					comboState = ComboStateFromEntity[RhythmEngineRelativeFromEntity[entity].Target];
				}

				state.MovementSpeed       = comboState.IsFever ? settings.FeverWalkSpeed : settings.BaseWalkSpeed;
				state.AttackSpeed         = settings.AttackSpeed;
				state.MovementAttackSpeed = settings.MovementAttackSpeed;
				state.Weight              = settings.Weight;
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return new JobUpdate
			{
				RhythmEngineRelativeFromEntity = GetComponentDataFromEntity<Relative<RhythmEngineDescription>>(true),
				ComboStateFromEntity           = GetComponentDataFromEntity<GameComboState>(true)
			}.Schedule(this, inputDeps);
		}
	}
}