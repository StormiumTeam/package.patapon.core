using System;
using System.Collections.Generic;
using System.Linq;
using GameBase.Roles.Components;
using package.stormiumteam.shared.ecs;
using Patapon.Client.Systems;
using StormiumTeam.GameBase.BaseSystems;
using StormiumTeam.GameBase.Utility.AssetBackend;
using StormiumTeam.GameBase.Utility.Pooling;
using StormiumTeam.GameBase.Utility.Pooling.BaseSystems;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using EntityQuery = Unity.Entities.EntityQuery;

namespace DataScripts.Models.GameMode.Structures
{
	public class HeadOnStructurePresentation : RuntimeAssetPresentation<HeadOnStructurePresentation>
	{
		public enum EPhase
		{
			Normal    = 0,
			Captured  = 1,
			Destroyed = 2
		}

		public MaterialPropertyBlock mpb;
		public List<Renderer>        rendererArray;
		public List<Renderer>        rendererWithTeamColorArray;

		public List<Animator> animators;

		[Header("Shader Properties")]
		public string teamTintPropertyId = "_Color";

		[Header("Animator Properties")]
		public string onIdleAnimTrigger = "OnIdle";

		public string onCapturedAnimTrigger  = "OnCaptured";
		public string onDestroyedAnimTrigger = "OnDestroyed";
		public string phaseAnimInt           = "Phase";

		public AudioClip onCaptureSound;
		public ECSoundEmitterComponent onCaptureSoundEmitter = new ECSoundEmitterComponent
		{
			volume       = 1,
			spatialBlend = 0,
			position     = 0,
			minDistance  = 20,
			maxDistance  = 40,
			rollOf       = AudioRolloffMode.Logarithmic
		};
		
		public AudioClip onDestroySound;
		public ECSoundEmitterComponent onDestroySoundEmitter = new ECSoundEmitterComponent
		{
			volume       = 1,
			spatialBlend = 0,
			position     = 0,
			minDistance  = 15,
			maxDistance  = 30,
			rollOf       = AudioRolloffMode.Logarithmic
		};

		public  bool                       GetPropertiesFromChildren = false;
		private List<MaterialPropertyBase> m_MaterialProperties;

		private Renderer[] m_computedRenderersTeamArray;
		private Renderer[] m_computedRenderersNormalArray;

		protected virtual void OnEnable()
		{
			mpb                  = new MaterialPropertyBlock();
			m_MaterialProperties = new List<MaterialPropertyBase>();
			foreach (var comp in GetPropertiesFromChildren
				? GetComponentsInChildren<MaterialPropertyBase>()
				: GetComponents<MaterialPropertyBase>())
				m_MaterialProperties.Add(comp);

			var computedRenderersTeamArray = new List<Renderer>(16);
			computedRenderersTeamArray.AddRange(rendererWithTeamColorArray);
			m_computedRenderersTeamArray = computedRenderersTeamArray.ToArray();

			var computedRenderersNormalArray = new List<Renderer>(16);
			computedRenderersNormalArray.AddRange(rendererArray);
			m_computedRenderersNormalArray = computedRenderersNormalArray.Except(m_computedRenderersTeamArray).ToArray();

			// copy mat
			void createmat(Renderer[] renders)
			{
				foreach (var r in renders)
				{
					if (r.sharedMaterial != null)
						r.material = new Material(r.sharedMaterial);
				}
			}

			createmat(m_computedRenderersNormalArray);
			createmat(m_computedRenderersTeamArray);
		}

		protected virtual void OnDisable()
		{
			mpb.Clear();
			mpb = null;
		}

		private void OnDestroy()
		{
			foreach (var r in rendererArray)
			{
				r.SetPropertyBlock(null);
				Destroy(r.material);
			}

			foreach (var r in rendererWithTeamColorArray)
			{
				r.SetPropertyBlock(null);
				Destroy(r.material);
			}
		}

		private Color m_TeamColor;

		public virtual void SetTeamColor(Color color)
		{
			m_TeamColor = color;
		}

		public virtual void Render()
		{
			mpb.Clear();

			foreach (var materialProperty in m_MaterialProperties)
			{
				materialProperty.RenderOn(mpb);
			}

			foreach (var r in m_computedRenderersNormalArray)
			{
				r.GetPropertyBlock(mpb);
				foreach (var materialProperty in m_MaterialProperties)
				{
					materialProperty.RenderOn(mpb);
				}

				r.SetPropertyBlock(mpb);
			}

			foreach (var r in m_computedRenderersTeamArray)
			{
				r.GetPropertyBlock(mpb);
				mpb.SetColor(teamTintPropertyId, m_TeamColor);
				foreach (var materialProperty in m_MaterialProperties)
				{
					materialProperty.RenderOn(mpb);
				}

				r.SetPropertyBlock(mpb);
			}
		}

		private EPhase m_PreviousPhase;

		public virtual void SetPhase(EPhase phase, bool sameTeam)
		{
			foreach (var a in animators) a.SetInteger(phaseAnimInt, (int) phase);

			if (m_PreviousPhase != phase)
			{
				AudioClip clipToPlay = null;
				ECSoundEmitterComponent emitter = default;
				
				var trigger = string.Empty;
				if (phase == EPhase.Normal)
					trigger = onIdleAnimTrigger;
				if (phase == EPhase.Captured)
				{
					trigger = onCapturedAnimTrigger;
					if (sameTeam)
					{
						clipToPlay = onCaptureSound;
						emitter    = onCaptureSoundEmitter;
					}
				}

				if (phase == EPhase.Destroyed)
				{
					trigger = onDestroyedAnimTrigger;
					clipToPlay = onDestroySound;
					emitter = onDestroySoundEmitter;
				}

				if (trigger != string.Empty)
					foreach (var a in animators)
						a.SetTrigger(trigger);

				m_PreviousPhase = phase;

				if (clipToPlay != null)
				{
					var entityManager = Backend.DstEntityManager;
					var world = entityManager.World;
					
					var soundDef = world.GetExistingSystem<ECSoundSystem>().ConvertClip(clipToPlay);
					if (soundDef.IsValid)
					{
						var soundEntity = entityManager.CreateEntity(typeof(ECSoundEmitterComponent), typeof(ECSoundDefinition), typeof(ECSoundOneShotTag));
						emitter.position = transform.position;
						
						Debug.LogError("play: " + clipToPlay);

						entityManager.SetComponentData(soundEntity, emitter);
						entityManager.SetComponentData(soundEntity, soundDef);
					}
				}
			}
		}
	}

	public class HeadOnStructureBackend : RuntimeAssetBackend<HeadOnStructurePresentation>
	{
		public bool HasTeam;
	}

	[UpdateInGroup(typeof(OrderGroup.Presentation.AfterSimulation))]
	public class HeadOnStructureRenderSystem : BaseRenderSystem<HeadOnStructureBackend>
	{
		public Entity PlayerTeam;
		
		protected override void PrepareValues()
		{
			var camState = this.GetComputedCameraState().StateData;
			if (EntityManager.TryGetComponentData(camState.Target, out Relative<TeamDescription> relativeTeam))
			{
				PlayerTeam = relativeTeam.Target;
			}
		}

		protected override void Render(HeadOnStructureBackend definition)
		{
			if (definition.Presentation == null)
				return;

			var presentation = definition.Presentation;
			var dstEntity    = definition.DstEntity;

			var direction = 1;
			var sameTeam = false;
			if (EntityManager.TryGetComponentData(dstEntity, out Relative<TeamDescription> teamDesc))
			{
				sameTeam = teamDesc.Target == PlayerTeam;
				
				if (teamDesc.Target == default || !EntityManager.TryGetComponentData<Relative<ClubDescription>>(teamDesc.Target, out var relativeClub))
				{
					definition.HasTeam = false;
					presentation.SetTeamColor(Color.white);
				}
				else
				{
					var clubInfo = EntityManager.GetComponentData<ClubInformation>(relativeClub.Target);
					presentation.SetTeamColor(clubInfo.PrimaryColor);

					if (EntityManager.TryGetComponentData<UnitDirection>(teamDesc.Target, out var teamDirection))
						direction = teamDirection.Value;

					definition.HasTeam = true;
				}
			}
			
			EntityManager.TryGetComponentData(dstEntity, out LivableHealth health);

			var phase = HeadOnStructurePresentation.EPhase.Normal;
			if (definition.HasTeam)
				phase = HeadOnStructurePresentation.EPhase.Captured;
			if (health.IsDead && definition.HasTeam)
				phase = HeadOnStructurePresentation.EPhase.Destroyed;

			presentation.SetPhase(phase, sameTeam);
			
			var pos = EntityManager.GetComponentData<Translation>(dstEntity).Value;
			pos.z += 300;

			definition.transform.position   = pos;
			definition.transform.localScale = new Vector3(direction, 1, 1);

			presentation.Render();
			presentation.OnSystemUpdate();
		}

		protected override void ClearValues()
		{

		}
	}

	[AlwaysSynchronizeSystem]
	[UpdateInWorld(UpdateInWorld.TargetWorld.Client)]
	public class SetHeadOnStructurePresentation : AbsGameBaseSystem
	{
		private AsyncAssetPool<GameObject> defaultWallPool;
		private AsyncAssetPool<GameObject> defaultTowerPool;
		private AsyncAssetPool<GameObject> defaultControlTowerPool;

		protected override void OnCreate()
		{
			base.OnCreate();

			var builder = AddressBuilder.Client()
			                            .Folder("Models")
			                            .Folder("GameModes")
			                            .Folder("Structures");

			defaultWallPool  = new AsyncAssetPool<GameObject>(builder.Folder("WoodBarricade").GetFile("WoodenWall.prefab"));
			defaultTowerPool = new AsyncAssetPool<GameObject>(builder.Folder("CobblestoneBarricade").GetFile("CobblestoneBarricade.prefab"));
			defaultControlTowerPool = new AsyncAssetPool<GameObject>(builder.Folder("CaptureTower").GetFile("CaptureTower.prefab"));
		}

		protected override void OnUpdate()
		{
			Entities.ForEach((HeadOnStructureBackend backend) =>
			{
				if (backend.HasIncomingPresentation)
					return;

				var poolDest = default(NativeString512);
				if (EntityManager.TryGetComponentData(backend.DstEntity, out TargetSceneAsset sceneAsset))
				{
					poolDest = sceneAsset.Str;
					throw new NotImplementedException("dynamic pool loading isn't done yet...");
				}

				AsyncAssetPool<GameObject> pool = null;
				if (EntityManager.TryGetComponentData(backend.DstEntity, out HeadOnStructure structure))
				{
					pool = StaticSceneResourceHolder.GetPool($"versus:{structure.ScoreType}");
					if (pool == null)
					{
						switch (structure.ScoreType)
						{
							case HeadOnStructure.EScoreType.TowerControl:
								pool = defaultControlTowerPool;
								break;
							case HeadOnStructure.EScoreType.Tower:
								pool = defaultTowerPool;
								break;
							case HeadOnStructure.EScoreType.Wall:
								pool = defaultWallPool;
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}
					}
				}

				if (pool == null)
					return;

				backend.SetPresentationFromPool(pool);
			}).WithStructuralChanges().Run();
		}
	}

	[UpdateInWorld(UpdateInWorld.TargetWorld.Client)]
	public class HeadOnStructurePoolSystem : PoolingSystem<HeadOnStructureBackend, HeadOnStructurePresentation>
	{
		protected override string AddressableAsset            => string.Empty;
		protected override Type[] AdditionalBackendComponents => new Type[] {typeof(SortingGroup)};

		protected override EntityQuery GetQuery()
		{
			return GetEntityQuery(typeof(HeadOnStructure));
		}

		protected override void SpawnBackend(Entity target)
		{
			base.SpawnBackend(target);

			var sortingGroup = LastBackend.GetComponent<SortingGroup>();
			sortingGroup.sortingLayerName = "MovableStructures";
			sortingGroup.sortingOrder     = 0;
		}
	}
}