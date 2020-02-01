using System;
using DefaultNamespace;
using package.stormiumteam.shared.ecs;
using Patapon.Mixed.GameModes.VSHeadOn;
using Patapon.Mixed.GamePlay;
using Revolution;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Components;
using StormiumTeam.GameBase.Modules;
using StormiumTeam.GameBase.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DataScripts.Models.GameMode.Structures
{
	public class CaptureAreaPresentation : RuntimeAssetPresentation<CaptureAreaPresentation>
	{
		public enum EPhase
		{
			Normal = 0,
			Captured = 1,
			Destroyed = 2
		}
		
		public GameObject poleRootLeft;
		public GameObject poleRootRight;

		public Renderer[] renderersForTeamColor;
		public Animator[] animators;

		public MaterialPropertyBlock mpb;
		
		private static readonly int Tint1PropertyId = Shader.PropertyToID("_Color");
		private static readonly int Tint2PropertyId = Shader.PropertyToID("_OverlayColor");
		private static readonly int PhaseStrHash = Animator.StringToHash("Phase");

		private void OnEnable()
		{
			mpb = new MaterialPropertyBlock();
		}

		public void SetTeamColor(Color color)
		{
			foreach (var r in renderersForTeamColor)
			{
				r.GetPropertyBlock(mpb);
				mpb.SetColor(Tint1PropertyId, color);
				mpb.SetColor(Tint2PropertyId, color);
				r.SetPropertyBlock(mpb);
			}
		}

		public void SetAreaSize(float size)
		{
			poleRootLeft.transform.localPosition = new Vector3 {x = -size * 0.5f};
			poleRootRight.transform.localPosition = new Vector3 {x = size * 0.5f};
		}

		public void SetPhase(EPhase phase, bool trigger)
		{
			foreach (var animator in animators)
			{
				animator.SetInteger(PhaseStrHash, (int)phase);
			}
		}

		private void OnDisable()
		{
			mpb.Clear();
			mpb = null;
		}
	}

	public class CaptureAreaBackend : RuntimeAssetBackend<CaptureAreaPresentation>
	{
		[NonSerialized]
		public int[] LastProgression = new int[2];

		public double LastCapturingTime;
	}

	[UpdateInWorld(UpdateInWorld.TargetWorld.Client)]
	[UpdateInGroup(typeof(OrderGroup.Simulation.SpawnEntities))]
	public class SpawnSystem : PoolingSystem<CaptureAreaBackend, CaptureAreaPresentation, SpawnSystem.CheckValid>
	{
		public struct CheckValid : ICheckValidity
		{
			[ReadOnly] public ComponentDataFromEntity<CaptureAreaComponent> AreaComponentFromEntity;
			
			public void OnSetup(ComponentSystemBase system)
			{
				AreaComponentFromEntity = system.GetComponentDataFromEntity<CaptureAreaComponent>(true);
			}

			public bool IsValid(Entity target)
			{
				return AreaComponentFromEntity[target].CaptureType == CaptureAreaType.Progressive;
			}
		}
		
		protected override string AddressableAsset =>
			AddressBuilder.Client()
			              .Folder("Models")
			              .Folder("GameModes")
			              .Folder("Structures")
			              .Folder("AreaControl")
			              .GetFile("CaptureArea.prefab");

		protected override EntityQuery GetQuery()
		{
			return GetEntityQuery(typeof(HeadOnStructure), typeof(CaptureAreaComponent));
		}
	}

	[UpdateInGroup(typeof(OrderGroup.Presentation.AfterSimulation))]
	public unsafe class RenderSystem : BaseRenderSystem<CaptureAreaPresentation>
	{
		public ClubInformation[] Clubs;
		
		private EntityQuery m_GameModeQuery;

		protected override void PrepareValues()
		{
			if (m_GameModeQuery == null)
			{
				m_GameModeQuery = GetEntityQuery(typeof(ReplicatedEntity), typeof(MpVersusHeadOn));
				Clubs = new ClubInformation[2];
			}

			if (!m_GameModeQuery.IsEmptyIgnoreFilter)
			{
				var gameMode = EntityManager.GetComponentData<MpVersusHeadOn>(m_GameModeQuery.GetSingletonEntity());
				for (var i = 0; i != 2; i++)
				{
					var team = i == 0 ? gameMode.Team0 : gameMode.Team1;

					EntityManager.TryGetComponentData(team, out var relativeClub, new Relative<ClubDescription>(team));
					EntityManager.TryGetComponentData(relativeClub.Target, out Clubs[i], new ClubInformation
					{
						Name           = new NativeString64("No Team"),
						PrimaryColor   = Color.cyan,
						SecondaryColor = Color.yellow
					});
				}
			}
		}

		protected override void Render(CaptureAreaPresentation definition)
		{
			var backend   = (CaptureAreaBackend) definition.Backend;
			var dstEntity = backend.DstEntity;

			if (EntityManager.TryGetComponentData(dstEntity, out CaptureAreaComponent area))
			{
				definition.SetAreaSize(area.Aabb.Extents.x);
				definition.transform.position = new Vector3
				{
					x = area.Aabb.Center.x,
					z = 250
				};
			}

			var targetPhase = CaptureAreaPresentation.EPhase.Normal;
			
			if (EntityManager.TryGetComponentData(dstEntity, out Relative<TeamDescription> teamRelative)
			    && teamRelative.Target != default
			    && EntityManager.TryGetComponentData(teamRelative.Target, out Relative<ClubDescription> clubRelative)
			    && EntityManager.TryGetComponentData(clubRelative.Target, out ClubInformation clubInfo))
			{
				definition.SetTeamColor(clubInfo.PrimaryColor);
				targetPhase = CaptureAreaPresentation.EPhase.Captured;
			}
			else if (EntityManager.TryGetComponentData(dstEntity, out HeadOnStructure structure))
			{
				if (backend.LastCapturingTime + 0.5 < Time.ElapsedTime)
					definition.SetTeamColor(Color.white);

				var progress = new Span<int>(structure.CaptureProgress, 2);
				for (var i = 0; i != 2; i++)
				{
					if (progress[i] > progress[1 - i] && progress[i] != backend.LastProgression[i])
					{
						definition.SetTeamColor(Clubs[i].PrimaryColor);
						backend.LastCapturingTime = Time.ElapsedTime;
					}
				}
				
				progress.CopyTo(backend.LastProgression);
			}

			if (EntityManager.TryGetComponentData(dstEntity, out LivableHealth health) && health.IsDead)
				targetPhase = CaptureAreaPresentation.EPhase.Destroyed;
			
			if (targetPhase == CaptureAreaPresentation.EPhase.Destroyed)
				definition.SetTeamColor(Color.black);
			definition.SetPhase(targetPhase, false);
		}

		protected override void ClearValues()
		{
			
		}
	}
}