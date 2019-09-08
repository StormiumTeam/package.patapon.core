using GmMachine;
using GmMachine.Blocks;
using Misc.GmMachine.Blocks;
using Misc.GmMachine.Contexts;
using package.patapon.core;
using package.stormiumteam.shared.ecs;
using Patapon4TLB.Core;
using Patapon4TLB.Default;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Components;
using StormiumTeam.GameBase.EcsComponents;
using Unity.Collections;
using Unity.Entities;
using Revolution.NetCode;
using Unity.Transforms;

namespace Patapon4TLB.GameModes
{
	public partial class MpVersusHeadOnGameMode
	{
		public class StartMapBlock : BlockCollection
		{
			public Block            SpawnBlock;
			public WaitingTickBlock ShowMapTextBlock;

			public StartMapBlock(string name) : base(name)
			{
				Add(SpawnBlock       = new Block("Test block"));
				Add(ShowMapTextBlock = new WaitingTickBlock("Time to show map start text"));
			}

			protected override bool OnRun()
			{
				if (RunNext(SpawnBlock))
				{
					EntityQuery currQuery;

					var queries   = Context.GetExternal<VersusHeadOnQueriesContext>();
					var gmContext = Context.GetExternal<VersusHeadOnContext>();
					var worldCtx  = Context.GetExternal<WorldContext>();

					// ----------------------------- //
					// Get spawn points
					currQuery = queries.SpawnPoint;
					using (var entities = currQuery.ToEntityArray(Allocator.TempJob))
					using (var teamTargetArray = currQuery.ToComponentDataArray<HeadOnTeamTarget>(Allocator.TempJob))
					{
						for (int ent = 0, length = entities.Length; ent < length; ent++)
						{
							var tTarget = teamTargetArray[ent];
							if (tTarget.TeamIndex < 0)
								continue;

							ref var team = ref gmContext.Teams[tTarget.TeamIndex];
							team.SpawnPoint = entities[ent];
						}
					}

					// ----------------------------- //
					// Get flags
					currQuery = queries.Flag;
					using (var entities = currQuery.ToEntityArray(Allocator.TempJob))
					using (var teamTargetArray = currQuery.ToComponentDataArray<HeadOnTeamTarget>(Allocator.TempJob))
					{
						for (int ent = 0, length = entities.Length; ent < length; ent++)
						{
							var tTarget = teamTargetArray[ent];
							if (tTarget.TeamIndex < 0)
								continue;

							ref var team = ref gmContext.Teams[tTarget.TeamIndex];
							team.Flag = entities[ent];
						}
					}

					// ----------------------------- //
					// Destroy rhythm engines
					worldCtx.EntityMgr.DestroyEntity(queries.GetEntityQueryBuilder().WithAll<RhythmEngineDescription, Relative<PlayerDescription>>().ToEntityQuery());

					// ----------------------------- //
					// Add players
					worldCtx.EntityMgr.AddComponent(queries.PlayerWithoutGameModeData, typeof(VersusHeadOnPlayer));

					// ----------------------------- //
					// Set team of players
					// Create player rhythm engines
					// Create player unit targets
					queries.GetEntityQueryBuilder().With(queries.Player).ForEach(player =>
					{
						var entMgr               = worldCtx.EntityMgr;
						var rhythmEngineProvider = worldCtx.GetOrCreateSystem<RhythmEngineProvider>();
						using (var spawnEntities = new NativeList<Entity>(Allocator.TempJob))
						{
							rhythmEngineProvider.SpawnLocalEntityWithArguments(new RhythmEngineProvider.Create
							{
								UseClientSimulation = true
							}, spawnEntities);

							entMgr.SetOrAddComponentData(spawnEntities[0], entMgr.GetComponentData<NetworkOwner>(player));
							entMgr.SetOrAddComponentData(spawnEntities[0], new RhythmEngineProcess {StartTime = worldCtx.GetExistingSystem<ServerSimulationSystemGroup>().GetTick().Ms});
							entMgr.AddComponent(spawnEntities[0], typeof(GhostComponent));

							entMgr.ReplaceOwnerData(spawnEntities[0], player);
						}

						var unitTarget = entMgr.CreateEntity(typeof(UnitTargetDescription), typeof(Translation), typeof(LocalToWorld), typeof(Relative<PlayerDescription>));
						entMgr.AddComponent(unitTarget, typeof(GhostComponent));
						entMgr.ReplaceOwnerData(unitTarget, player);
					});

					// ----------------------------- //
					// Create towers
					queries.GetEntityQueryBuilder().ForEach((Entity entity, ref HeadOnStructure headOnStructure) =>
					{
						// create health entity
						var healthProvider = worldCtx.GetExistingSystem<DefaultHealthData.InstanceProvider>();
						var healthEntity = healthProvider.SpawnLocalEntityWithArguments(new DefaultHealthData.CreateInstance
						{
							max   = 0,
							value = 0,
							owner = entity
						});
						worldCtx.EntityMgr.AddComponent(healthEntity, typeof(GhostComponent));
						worldCtx.EntityMgr.SetOrAddComponentData(entity, new LivableHealth
						{
							IsDead = false
						});
					});
					
					ShowMapTextBlock.SetTicksFromMs(10);

					return false;
				}

				if (RunNext(ShowMapTextBlock))
					return false;

				return true;
			}

			protected override void OnReset()
			{
				base.OnReset();

				ShowMapTextBlock.TickGetter = Context.GetExternal<VersusHeadOnContext>();
			}
		}
	}
}