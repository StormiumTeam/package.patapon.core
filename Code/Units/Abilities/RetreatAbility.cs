using Patapon4TLB.Core;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Patapon4TLB.Default
{
	public struct RetreatAbility : IComponentData
	{
		public int LastActiveId;

		public float  AccelerationFactor;
		public float3 StartPosition;
		public float  BackVelocity;
		public bool   IsRetreating;
		public float  ActiveTime;
	}

	[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
	public class RetreatAbilitySystem : JobGameBaseSystem
	{
		private struct Job : IJobForEach<Owner, RhythmAbilityState, RetreatAbility>
		{
			[ReadOnly] public float DeltaTime;

			[ReadOnly] public ComponentDataFromEntity<Translation>      TranslationFromEntity;
			[ReadOnly] public ComponentDataFromEntity<UnitBaseSettings> UnitSettingsFromEntity;
			[ReadOnly] public ComponentDataFromEntity<UnitDirection>    UnitDirectionFromEntity;

			[NativeDisableParallelForRestriction] public ComponentDataFromEntity<UnitControllerState> UnitControllerStateFromEntity;
			[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Velocity>            VelocityFromEntity;

			public void Execute(ref Owner owner, ref RhythmAbilityState state, ref RetreatAbility ability)
			{
				if (state.ActiveId != ability.LastActiveId)
				{
					ability.IsRetreating = false;
					ability.ActiveTime   = 0;
					ability.LastActiveId = state.ActiveId;
				}

				if (!state.IsActive && !state.IsStillChaining)
				{
					ability.ActiveTime   = 0;
					ability.IsRetreating = false;
					return;
				}

				var wasRetreating = ability.IsRetreating;
				ability.IsRetreating = ability.ActiveTime <= 2.0f;

				var translation   = TranslationFromEntity[owner.Target];
				var unitSettings  = UnitSettingsFromEntity[owner.Target];
				var unitDirection = UnitDirectionFromEntity[owner.Target];
				var velocity      = VelocityFromEntity[owner.Target];

				var retreatSpeed = unitSettings.MovementAttackSpeed * 2f;

				if (!wasRetreating && ability.IsRetreating)
				{
					ability.StartPosition = translation.Value;
					velocity.Value.x      = -unitDirection.Value * retreatSpeed;
				}

				if (!ability.IsRetreating)
				{
					if (wasRetreating)
					{
						ability.BackVelocity = math.abs(ability.StartPosition.x - translation.Value.x);
					}

					var newPosX = Mathf.MoveTowards(translation.Value.x, ability.StartPosition.x, ability.BackVelocity * DeltaTime);
					velocity.Value.x = (newPosX - translation.Value.x) / DeltaTime;
				}

				ability.ActiveTime += DeltaTime;

				VelocityFromEntity[owner.Target] = velocity;

				var controllerState = UnitControllerStateFromEntity[owner.Target];
				controllerState.ControlOverVelocity         = ability.IsRetreating;
				//controllerState.OverrideTargetPosition      = true;
				//controllerState.TargetPosition              = ability.StartPosition;
				UnitControllerStateFromEntity[owner.Target] = controllerState;
			}
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return new Job
			{
				DeltaTime                     = GetSingleton<GameTimeComponent>().DeltaTime,
				TranslationFromEntity         = GetComponentDataFromEntity<Translation>(true),
				UnitSettingsFromEntity        = GetComponentDataFromEntity<UnitBaseSettings>(true),
				UnitDirectionFromEntity       = GetComponentDataFromEntity<UnitDirection>(true),
				UnitControllerStateFromEntity = GetComponentDataFromEntity<UnitControllerState>(),
				VelocityFromEntity            = GetComponentDataFromEntity<Velocity>()
			}.Schedule(this, inputDeps);
		}
	}

	public class RetreatAbilityProvider : BaseProviderBatch<RetreatAbilityProvider.Create>
	{
		public struct Create
		{
			public Entity Owner;
			public Entity Command;
			public float  AccelerationFactor;
		}

		public override void GetComponents(out ComponentType[] entityComponents)
		{
			entityComponents = new ComponentType[]
			{
				typeof(ActionDescription),
				typeof(RhythmAbilityState),
				typeof(RetreatAbility),
				typeof(Owner),
				typeof(DestroyChainReaction)
			};
		}

		public override void SetEntityData(Entity entity, Create data)
		{
			EntityManager.ReplaceOwnerData(entity, data.Owner);
			EntityManager.SetComponentData(entity, new RhythmAbilityState {Command        = data.Command});
			EntityManager.SetComponentData(entity, new RetreatAbility {AccelerationFactor = data.AccelerationFactor});
			EntityManager.SetComponentData(entity, new Owner {Target                      = data.Owner});
			EntityManager.SetComponentData(entity, new DestroyChainReaction(data.Owner));
		}
	}
}