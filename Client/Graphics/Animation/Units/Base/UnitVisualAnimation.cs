using System;
using System.Collections.Generic;
using package.stormiumteam.shared.ecs;
using PataNext.Module.Simulation.Components.Roles;
using StormiumTeam.GameBase.Utility.AssetBackend;
using StormiumTeam.GameBase.Utility.AssetBackend.Components;
using StormiumTeam.GameBase.Utility.Pooling.BaseSystems;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PataNext.Client.Graphics.Animation.Units.Base
{
	public abstract class UnitVisualPresentation : RuntimeAssetPresentation<UnitVisualPresentation>
	{
		public Animator                                     Animator;

		/// <summary>
		/// Return the folders for animation.
		/// </summary>
		/// <remarks>
		///	The order is the OverrideObject then folders.
		/// If the first folder does not contains the animation, it will search it on the next.
		/// </remarks>
		public AssetLabelReference[] animationAssetLabels;

		/// <summary>
		/// Get the cache for animation. If empty, animations will not be re-used
		/// </summary>
		public string animationCacheId;

		public abstract void UpdateData();

		private void Start()
		{
			Addressables.InitializeAsync().Completed += handle =>
			{
				foreach (var locator in Addressables.ResourceLocators)
				{
					Debug.Log($"{locator.GetType()}; {locator.LocatorId}");
					foreach (var key in locator.Keys)
					{
						Debug.Log($"\t{key}");
						if (locator.Locate(key, null, out var locations))
							foreach (var loc in locations)
								Debug.Log($"{loc.PrimaryKey}; {loc.InternalId}; {loc.ResourceType}");
					}
				}
			};
		}
	}

	public class UnitVisualPlayableBehaviourData : PlayableBehaviorData
	{
		public UnitVisualAnimation VisualAnimation;
		public TargetAnimation     CurrAnimation => VisualAnimation.CurrAnimation;
		public double              RootTime      => VisualAnimation.RootTime;
	}

	public class UnitVisualAnimation : VisualAnimation
	{
		public double                 RootTime     => rootMixer.GetTime();
		public UnitVisualBackend      Backend      { get; private set; }
		public UnitVisualPresentation Presentation { get; private set; }

		public TargetAnimation CurrAnimation { get; private set; } = new TargetAnimation(null);

		public void OnDisable()
		{
			DestroyPlayableGraph();
		}

		public void OnBackendSet(UnitVisualBackend backend)
		{
			Backend = backend;

			DestroyPlayableGraph();
			CreatePlayableGraph($"{backend.DstEntity}");
			CreatePlayable();

			m_PlayableGraph.Stop();
		}

		public void OnPresentationSet(UnitVisualPresentation presentation)
		{
			Presentation = presentation;
			
			// reset graph ofc when getting a new presentation
			DestroyPlayableGraph();
			CreatePlayableGraph($"{Backend.DstEntity}");
			CreatePlayable();
			
			SetAnimatorOutput("standard output", presentation.Animator);

			CurrAnimation = new TargetAnimation(null);
			
			m_PlayableGraph.Stop();
			m_PlayableGraph.Play();
		}

		public void SetTargetAnimationWithTypeKeepTransition(Type type)
		{
			CurrAnimation = new TargetAnimation(type,
				transitionStart: CurrAnimation.TransitionStart, transitionEnd: CurrAnimation.TransitionEnd,
				previousType: CurrAnimation.Type);
		}

		public void SetTargetAnimation(TargetAnimation target)
		{
			CurrAnimation = target;
		}

		public UnitVisualPlayableBehaviourData GetBehaviorData()
		{
			return new UnitVisualPlayableBehaviourData
			{
				DstEntity        = Backend.DstEntity,
				DstEntityManager = Backend.DstEntityManager,
				VisualAnimation  = this
			};
		}
	}

	public class UnitVisualBackend : RuntimeAssetBackend<UnitVisualPresentation>
	{
		public  string              CurrentArchetype;
		private UnitVisualAnimation m_Animation;

		public UnitVisualAnimation Animation => m_Animation;

		public override void OnTargetUpdate()
		{
			if (!TryGetComponent(out m_Animation))
			{
				m_Animation = gameObject.AddComponent<UnitVisualAnimation>();
				if (!DstEntityManager.HasComponent(BackendEntity, typeof(UnitVisualAnimation)))
				{
					DstEntityManager.AddComponentObject(BackendEntity, m_Animation);
				}
			}

			m_Animation.OnBackendSet(this);
			DstEntityManager.AddComponentData(BackendEntity, RuntimeAssetDisable.All);
		}

		public override void OnPresentationSet()
		{
			// set layer recursive...
			foreach (var tr in gameObject.GetComponentsInChildren<Transform>())
				tr.gameObject.layer = gameObject.layer;
			
			Presentation.Animator.WriteDefaultValues();
			Presentation.Animator.Rebind();
			Animation.OnPresentationSet(Presentation);
			Presentation.Animator.runtimeAnimatorController = null;
		}

		public override void ReturnPresentation(bool unsetChildren = true)
		{
			if (Presentation != null && Presentation.Animator != null)
			{
				Presentation.Animator.WriteDefaultValues();
				Presentation.Animator.Rebind();
			}
			base.ReturnPresentation(unsetChildren);
		}

		public override void OnReset()
		{
			if (m_Animation != null)
			{
				m_Animation.DestroyPlayableGraph();
				m_Animation = null;
			}

			CurrentArchetype = string.Empty;
		}
	}

	public struct UnitVisualSourceBackend : IComponentData
	{
		public Entity Backend;
	}
	
	public class UnitVisualBackendSpawnSystem : PoolingSystem<UnitVisualBackend, UnitVisualPresentation>
	{
		protected override string AddressableAsset => "core://Client/Models/UberHero/EmptyPresentation.prefab";

		protected override Type[] AdditionalBackendComponents => new Type[] {typeof(SortingGroup)};

		protected override EntityQuery GetQuery()
		{
			return GetEntityQuery(typeof(UnitDescription));
		}

		protected override void SpawnBackend(Entity target)
		{
			base.SpawnBackend(target);
			EntityManager.SetOrAddComponentData(target, new UnitVisualSourceBackend {Backend = LastBackend.BackendEntity});

			var sortingGroup = LastBackend.GetComponent<SortingGroup>();
			sortingGroup.sortingLayerName = "Entities";
			sortingGroup.sortingOrder     = 0;

			LastBackend.gameObject.layer = LayerMask.NameToLayer("Entities");
		}
	}
}