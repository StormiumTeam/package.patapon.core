using Systems;
using Misc;
using Misc.Extensions;
using package.stormiumteam.shared.ecs;
using Patapon.Client.RhythmEngine;
using Patapon.Mixed.GamePlay.RhythmEngine;
using Patapon.Mixed.RhythmEngine;
using Patapon.Mixed.RhythmEngine.Flow;
using RhythmEngine;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Components;
using StormiumTeam.GameBase.Misc;
using StormiumTeam.GameBase.Systems;
using StormiumTeam.Shared;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;

namespace package.patapon.core.FeverWorm
{
	public class FeverWormBackend : RuntimeAssetBackend<FeverWormPresentation>
	{
		private bool? m_IsEnabled;

		public void SetEnabled(bool value)
		{
			if (m_IsEnabled == null || m_IsEnabled.Value != value)
			{
				m_IsEnabled = value;
				Presentation.gameObject.SetActive(value);
			}
		}
	}

	public struct CreateFeverWormData : IComponentData
	{
	}

	[UpdateInGroup(typeof(OrderInterfaceSystemGroup))]
	public class FeverWormOrderingSystem : OrderingSystem
	{
	}

	// we do process on the backend instead of presentation since we do disable/enabled the presentation (aka removing it from the entity list)
	public class FeverWormRenderSystem : BaseRenderSystem<FeverWormBackend>
	{
		private Localization m_LocalTextDb;

		public string ComboString;

		public float SummonEnergyReal;
		public float InterpolatedEnergyReal;
		public float ComboScoreReal;

		public int ComboCount;

		public bool IsFever;

		public float Pulsation;

		private float real(int v, int m)
		{
			if (m == 0 || v == m)
				return 1f;
			if (v == 0)
				return 0f;
			return (float) v / m;
		}

		protected override void PrepareValues()
		{
			if (m_LocalTextDb == null)
			{
				m_LocalTextDb = World.GetOrCreateSystem<LocalizationSystem>()
				                     .LoadLocal("ingame");
			}

			ComboString = m_LocalTextDb["ComboText", "FWorm"];

			var player = this.GetFirstSelfGamePlayer();
			if (player == default)
				return;

			Entity engine;
			if (this.TryGetCurrentCameraState(player, out var camState))
				engine = PlayerComponentFinder.GetComponentFromPlayer<RhythmEngineDescription>(EntityManager, m_EngineQuery, camState.Target, player);
			else
				engine = PlayerComponentFinder.FindPlayerComponent(m_EngineQuery, player);

			if (engine == default)
				return;

			var comboState = EntityManager.GetComponentData<GameComboState>(engine);
			SummonEnergyReal = real(comboState.JinnEnergy, comboState.JinnEnergyMax);
			ComboScoreReal   = real(comboState.Score, 50); // todo: the magic number need to be removed!
			ComboCount       = comboState.Chain;
			IsFever          = comboState.IsFever;

			var process  = EntityManager.GetComponentData<FlowEngineProcess>(engine);
			var settings = EntityManager.GetComponentData<RhythmEngineSettings>(engine);
			Pulsation = real(process.Milliseconds % settings.BeatInterval, settings.BeatInterval);

			InterpolatedEnergyReal = Mathf.MoveTowards(math.lerp(InterpolatedEnergyReal, SummonEnergyReal, Time.DeltaTime), SummonEnergyReal, Time.DeltaTime * 0.25f);
			
			if (m_PlaySongSystem.Score != m_PreviousScore)
			{
				m_PreviousScoreInterpol = m_PreviousScore;
				m_PreviousScore = m_PlaySongSystem.Score;
			}
			m_PreviousScoreInterpol = Mathf.MoveTowards(math.lerp(m_PreviousScoreInterpol, m_PlaySongSystem.Score, Time.DeltaTime * 5), m_PlaySongSystem.Score, Time.DeltaTime);
		}

		protected override void Render(FeverWormBackend backend)
		{
			if (backend.Presentation == null)
				return;

			var definition = backend.Presentation;
			backend.SetEnabled(ComboCount >= 2);

			definition.SetComboString(ComboString);
			definition.SetProgression(ComboScoreReal, ComboCount, InterpolatedEnergyReal, IsFever);
			definition.SetColors(definition.currentPulse);

			definition.Animator.enabled = false;
			definition.Animator.SetFloat(_NtDoubleBeat, Pulsation);
			definition.Animator.SetFloat(_NtOneBeat, Pulsation);
			definition.Animator.SetFloat(_Score, m_PreviousScoreInterpol);
			definition.Animator.SetBool(_IsFever, IsFever);
			definition.Animator.Update(Time.DeltaTime);
		}

		private int m_PreviousScore;
		private float m_PreviousScoreInterpol;

		protected override void ClearValues()
		{
			// not used
		}

		private EntityQuery m_EngineQuery;
		private RhythmEnginePlaySong m_PlaySongSystem;
		private static readonly int _NtOneBeat = Animator.StringToHash("_NT1.0");
		private static readonly int _NtDoubleBeat = Animator.StringToHash("_NT0.5");
		private static readonly int _Score = Animator.StringToHash("_Score");
		private static readonly int _IsFever = Animator.StringToHash("_IsFever");

		protected override void OnCreate()
		{
			base.OnCreate();

			EntityManager.CreateEntity(typeof(CreateFeverWormData));
			
			m_EngineQuery = GetEntityQuery(typeof(RhythmEngineDescription), typeof(Relative<PlayerDescription>));
			m_PlaySongSystem = World.GetOrCreateSystem<RhythmEnginePlaySong>();
		}
	}

	[UpdateInWorld(UpdateInWorld.TargetWorld.Client)]
	public class FeverWormCreate : PoolingSystem<FeverWormBackend, FeverWormPresentation>
	{
		protected override string AddressableAsset => "core://Client/Interface/InGame/RhythmEngine/FeverWorm/FeverWorm.prefab";

		private Canvas m_Canvas;

		protected override EntityQuery GetQuery()
		{
			return GetEntityQuery(typeof(CreateFeverWormData));
		}

		protected override void SpawnBackend(Entity target)
		{
			if (m_Canvas == null)
			{
				var interfaceOrder = World.GetExistingSystem<FeverWormOrderingSystem>().Order;
				var canvasSystem   = World.GetExistingSystem<ClientCanvasSystem>();

				m_Canvas               = canvasSystem.CreateCanvas(out _, "FeverWormCanvas");
				m_Canvas.renderMode    = RenderMode.ScreenSpaceCamera;
				m_Canvas.worldCamera   = World.GetExistingSystem<ClientCreateCameraSystem>().Camera;
				m_Canvas.planeDistance = 1;
				m_Canvas.sortingLayerID = SortingLayer.NameToID("OverlayUI");
				m_Canvas.sortingOrder   = interfaceOrder;

				var scaler = m_Canvas.GetComponent<CanvasScaler>();
				scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = new Vector2(1920, 1080);
				scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
				scaler.matchWidthOrHeight  = 1;
			}

			base.SpawnBackend(target);

			var backend = LastBackend;
			backend.transform.SetParent(m_Canvas.transform, false);
			if (!backend.TryGetComponent(out RectTransform rt))
			{
				rt = backend.gameObject.AddComponent<RectTransform>();
			}

			rt.localScale       = new Vector3(70, 70, 1);
			rt.anchorMin        = new Vector2(0, 0.5f);
			rt.anchorMax        = new Vector2(0, 0.5f);
			rt.anchoredPosition = new Vector2(0, 250);
			rt.sizeDelta        = new Vector2(100, 100);
			rt.pivot            = new Vector2(0.5f, 0.5f);
		}
	}
}