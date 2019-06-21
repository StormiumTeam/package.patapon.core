using package.patapon.core;
using package.StormiumTeam.GameBase;
using Patapon4TLB.Core;
using Patapon4TLB.Default;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using CapsuleCollider = Unity.Physics.CapsuleCollider;

namespace Patapon4TLB.GameModes.Basic
{
	[DisableAutoCreation]
	public class BasicGameModeCreateUnitSystem : GameBaseSystem
	{
		private EntityArchetype m_UnitArchetype;

		protected override void OnCreate()
		{
			base.OnCreate();

			m_UnitArchetype = EntityManager.CreateArchetype
			(
				typeof(UnitDescription),

				typeof(UnitBaseSettings),
				typeof(UnitDirection),
				typeof(UnitRhythmState),

				typeof(Translation),
				typeof(Rotation),
				typeof(LocalToWorld),

				typeof(PhysicsCollider),
				typeof(PhysicsDamping),
				typeof(PhysicsMass),
				typeof(PhysicsVelocity), // right now, we don't have 2D Controller, so we need to use rigidBody based physics...
				typeof(GroundState),

				typeof(Relative<PlayerDescription>),
				typeof(Relative<RhythmEngineDescription>),
				typeof(Relative<TeamDescription>),

				typeof(ActionContainer),

				typeof(DestroyChainReaction)
			);
		}

		protected override void OnUpdate()
		{
			var gameMode = World.GetExistingSystem<BasicGameModeSystem>();
			for (var pl = 0; pl != gameMode.NewPlayers.Length; pl++)
			{
				var playerEntity = gameMode.NewPlayers[pl];
				var playerData   = EntityManager.GetComponentData<BasicGameModePlayer>(playerEntity);

				// Create a sample unit...
				var unit     = EntityManager.CreateEntity(m_UnitArchetype);
				var collider = CapsuleCollider.Create(new float3(0), new float3(0, 2, 0), 0.5f);

				EntityManager.SetComponentData(unit, new UnitBaseSettings
				{
					BaseWalkSpeed  = 3.5f,
					FeverWalkSpeed = 5f,
					Weight         = 5
				});
				EntityManager.SetComponentData(unit, UnitDirection.Right);
				EntityManager.SetComponentData(unit, new UnitRhythmState());
				EntityManager.SetComponentData(unit, PhysicsMass.CreateDynamic(collider.Value.MassProperties, 1));
				EntityManager.SetComponentData(unit, new PhysicsCollider
				{
					Value = collider
				});
				EntityManager.SetComponentData(unit, new PhysicsDamping
				{
					Linear = 0.1f
				});
				EntityManager.SetComponentData(unit, new GroundState(true));
				EntityManager.SetComponentData(unit, new Relative<PlayerDescription> {Target       = playerEntity});
				EntityManager.SetComponentData(unit, new Relative<RhythmEngineDescription> {Target = playerData.RhythmEngine});
				EntityManager.SetComponentData(unit, new DestroyChainReaction(playerEntity));

				var marchAbility = EntityManager.CreateEntity();
				// We should instead search for entities with 'MarchCommand' component tag...
				var marchCommand = World.GetExistingSystem<RhythmCommandBuilder>().GetOrCreate(new NativeArray<RhythmCommandSequence>(4, Allocator.TempJob)
				{
					[0] = new RhythmCommandSequence(0, RhythmKeys.Left),
					[1] = new RhythmCommandSequence(1, RhythmKeys.Left),
					[2] = new RhythmCommandSequence(2, RhythmKeys.Left),
					[3] = new RhythmCommandSequence(3, RhythmKeys.Right),
				});
				EntityManager.AddComponentData(marchAbility, new ActionDescription());
				EntityManager.AddComponentData(marchAbility, new RhythmAbilityState {Command      = marchCommand});
				EntityManager.AddComponentData(marchAbility, new MarchAbility {AccelerationFactor = 1});
				EntityManager.AddComponentData(marchAbility, new Owner {Target                    = unit});
				EntityManager.AddComponentData(marchAbility, new DestroyChainReaction(unit));

				playerData.Unit = unit;
				EntityManager.SetComponentData(playerEntity, playerData);

				Debug.Log($"Create entity with {unit} {playerData.RhythmEngine}");
			}
		}
	}
}