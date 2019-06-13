using System.Collections.Generic;
using package.patapon.core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Random = UnityEngine.Random;

namespace Patapon4TLB.Default.Test
{
	[UpdateInGroup(typeof(ClientPresentationSystemGroup))]
	[UpdateAfter(typeof(RhythmEngineClientInputSystem))]
	public class PlayBeatSound : ComponentSystem
	{
		private int  m_LastBeat;
		private bool m_Play;

		public bool VoiceOverlay;

		private void ForEachEngine(ref RhythmEngineProcess process)
		{
			if (m_LastBeat == process.Beat)
				return;

			m_LastBeat = process.Beat;
			m_Play     = true;
		}

		private void ForEachPressureEvent(Entity entity, ref PressureEvent pressureEvent)
		{
			if (!EntityManager.HasComponent(pressureEvent.Engine, typeof(RhythmEngineSimulateTag)))
				return;

			var absRealScore = math.abs(pressureEvent.Score);
			var score        = 0;
			if (absRealScore <= 0.15f)
				score = 0;
			else
				score = 1;

			m_AudioSourceOnNewPressureDrum.PlayOneShot(m_AudioOnPressureDrum[pressureEvent.Key][score]);
			if (VoiceOverlay)
			{
				var id = score;
				if (id > 0)
				{
					id = Mathf.Clamp(Random.Range(-1, 3), 1, 2); // more chance to have a 1
				}

				m_AudioSourceOnNewPressureVoice.PlayOneShot(m_AudioOnPressureVoice[pressureEvent.Key][id]);
			}
		}

		private EntityQueryBuilder.F_D<RhythmEngineProcess> m_EngineDelegate;
		private EntityQueryBuilder.F_ED<PressureEvent> m_PressureEventDelegate;

		private AudioSource m_AudioSourceOnNewBeat;
		private AudioSource m_AudioSourceOnNewPressureDrum;
		private AudioSource m_AudioSourceOnNewPressureVoice;

		private AudioClip                                   m_AudioOnNewBeat;
		private Dictionary<int, Dictionary<int, AudioClip>> m_AudioOnPressureDrum;
		private Dictionary<int, Dictionary<int, AudioClip>> m_AudioOnPressureVoice;

		protected override void OnCreate()
		{
			VoiceOverlay = true;
			
			AudioSource CreateAudioSource(string name, float volume)
			{
				var audioSource = new GameObject("(Clip) " + name, typeof(AudioSource)).GetComponent<AudioSource>();
				audioSource.reverbZoneMix = 0f;
				audioSource.spatialBlend  = 0f;
				audioSource.volume        = volume;

				return audioSource;
			}

			base.OnCreate();

			if (!Application.isPlaying)
				return;

			Addressables.InitializationOperation.Completed += op => { OnLoadAssets(); };

			m_EngineDelegate        = ForEachEngine;
			m_PressureEventDelegate = ForEachPressureEvent;

			m_AudioSourceOnNewBeat          = CreateAudioSource("On New Beat", 0.25f);
			m_AudioSourceOnNewPressureDrum  = CreateAudioSource("On New Pressure -> Drum", 0.33f);
			m_AudioSourceOnNewPressureVoice = CreateAudioSource("On New Pressure -> Voice", 0.33f);
		}

		protected void OnLoadAssets()
		{
			Addressables.LoadAsset<AudioClip>("int:RhythmEngine/Sounds/on_new_beat.ogg")
			            .Completed += op => m_AudioOnNewBeat = op.Result;

			m_AudioOnPressureDrum = new Dictionary<int, Dictionary<int, AudioClip>>(12);
			m_AudioOnPressureVoice = new Dictionary<int, Dictionary<int, AudioClip>>(12);

			for (int i = 0; i != 4; i++)
			{
				var key = i + 1;

				m_AudioOnPressureVoice[key] = new Dictionary<int, AudioClip>(3);
				m_AudioOnPressureDrum[key] = new Dictionary<int, AudioClip>(3);

				for (int r = 0; r != 3; r++)
				{
					var rank = r;

					m_AudioOnPressureDrum[key][rank] = null;
					m_AudioOnPressureVoice[key][rank] = null;

					Addressables.LoadAsset<AudioClip>($"int:RhythmEngine/Sounds/drum_{key}_{rank}.ogg").Completed += op =>
					{
						Debug.Assert(op.IsValid, "op.IsValid");

						m_AudioOnPressureDrum[key][rank] = op.Result;
					};
					
					Addressables.LoadAsset<AudioClip>($"int:RhythmEngine/Sounds/voice_{key}_{rank}.wav").Completed += op =>
					{
						Debug.Assert(op.IsValid, "op.IsValid");

						m_AudioOnPressureVoice[key][rank] = op.Result;
					};
				}
			}
		}

		protected override void OnUpdate()
		{
			if (!Application.isPlaying)
				return;
			
			m_Play = false;

			Entities.WithAll<RhythmEngineSimulateTag>().ForEach(m_EngineDelegate);

			if (m_Play)
			{
				m_AudioSourceOnNewBeat.PlayOneShot(m_AudioOnNewBeat);
			}

			Entities.ForEach(m_PressureEventDelegate);
		}
	}
}