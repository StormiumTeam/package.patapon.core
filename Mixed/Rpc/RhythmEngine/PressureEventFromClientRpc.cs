using package.stormiumteam.shared.ecs;
using Patapon.Mixed.GamePlay.RhythmEngine;
using Patapon.Mixed.RhythmEngine.Flow;
using Revolution;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.EcsComponents;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace Patapon.Mixed.RhythmEngine.Rpc
{
	[BurstCompile]
	public struct PressureEventFromClientRpc : IRpcCommand
	{
		public uint EngineGhostId;

		public float Score;
		public int  Key;
		public int  FlowBeat;
		public bool ShouldStartRecovery;

		public void Serialize(DataStreamWriter writer)
		{
			writer.Write(EngineGhostId);
			writer.Write(Key);
			writer.Write(Score);
			writer.Write(FlowBeat);
			writer.Write((byte) (ShouldStartRecovery ? 1 : 0));
		}

		public void Deserialize(DataStreamReader reader, ref DataStreamReader.Context ctx)
		{
			EngineGhostId       = reader.ReadUInt(ref ctx);
			Key                 = reader.ReadInt(ref ctx);
			Score               = reader.ReadFloat(ref ctx);
			FlowBeat            = reader.ReadInt(ref ctx);
			ShouldStartRecovery = reader.ReadByte(ref ctx) == 1;
		}

		[BurstCompile]
		private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
		{
			RpcExecutor.ExecuteCreateRequestComponent<PressureEventFromClientRpc>(ref parameters);
		}

		public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
		{
			return new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
		}
		
		public class RpcSystem : RpcCommandRequestSystem<PressureEventFromClientRpc> {}
	}
	
	[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
	public class ReceivePressureEventRpcSystem : JobGameBaseSystem
	{
		private EndSimulationEntityCommandBufferSystem m_EndBarrier;
		private EntityQuery                            m_EventQuery;
		private CreateSnapshotSystem                   m_CreateSnapshotSystem;

		protected override void OnCreate()
		{
			m_EventQuery           = GetEntityQuery(typeof(PressureEventFromClientRpc));
			m_EndBarrier           = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
			m_CreateSnapshotSystem = World.GetOrCreateSystem<CreateSnapshotSystem>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var playerRelativeFromEntity = GetComponentDataFromEntity<Relative<PlayerDescription>>(true);
			var networkOwnerFromEntity   = GetComponentDataFromEntity<NetworkOwner>(true);
			var processFromEntity        = GetComponentDataFromEntity<FlowEngineProcess>(false);
			var stateFromEntity          = GetComponentDataFromEntity<RhythmEngineState>(false);
			var comboFromEntity          = GetComponentDataFromEntity<GameComboState>(false);

			var ghostMap = m_CreateSnapshotSystem.GhostToEntityMap;

			var tick = World.GetExistingSystem<ServerSimulationSystemGroup>().ServerTick;

			inputDeps =
				Entities
					.ForEach((in PressureEventFromClientRpc ev, in ReceiveRpcCommandRequestComponent receiveData) =>
					{
						if (!ghostMap.TryGetValue(ev.EngineGhostId, out var ghostEntity))
							return;
						if (!playerRelativeFromEntity.TryGet(ghostEntity, out var playerRelative)
						    && !networkOwnerFromEntity.TryGet(playerRelative.Target, out var networkOwner)
						    && networkOwner.Value != receiveData.SourceConnection)
							return;

						var process = processFromEntity[ghostEntity];
						var combo   = comboFromEntity[ghostEntity];
						var state   = stateFromEntity[ghostEntity];
						
						if (math.abs(ev.Score) <= FlowPressure.Perfect)
						{
							combo.JinnEnergy += 20;
						}

						state.LastPressureBeat = math.min(ev.Key, process.GetFlowBeat(process.Milliseconds));

						comboFromEntity[ghostEntity] = combo;
						stateFromEntity[ghostEntity] = state;
					})
					.WithReadOnly(ghostMap)
					.WithReadOnly(processFromEntity)
					.WithReadOnly(playerRelativeFromEntity)
					.WithReadOnly(networkOwnerFromEntity)
					.WithNativeDisableParallelForRestriction(stateFromEntity)
					.WithNativeDisableParallelForRestriction(comboFromEntity)
					.Schedule(inputDeps);

			m_EndBarrier.CreateCommandBuffer().DestroyEntity(m_EventQuery);
			m_EndBarrier.AddJobHandleForProducer(inputDeps);

			return inputDeps;
		}
	}
}