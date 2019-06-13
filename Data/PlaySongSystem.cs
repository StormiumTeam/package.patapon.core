using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using package.patapon.core;
using Patapon4TLB.Core;
using Patapon4TLB.Core.json;
using StormiumTeam.GameBase;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Patapon4TLB.Default.Test
{
	[UpdateInGroup(typeof(ClientPresentationSystemGroup))]
	public class PlaySongClientSystem : GameBaseSystem
	{
		public int Beat;
		public int Interval;
		public int Tick;

		public bool HasActiveRhythmEngine;
		
		protected override void OnUpdate()
		{
			HasActiveRhythmEngine = false;
			Entities.WithAll<RhythmEngineSimulateTag>().ForEach((ref RhythmEngineSettings settings, ref RhythmEngineState state, ref RhythmEngineProcess process) =>
			{
				Beat = process.Beat;
				Tick = process.TimeTick;
				Interval = settings.BeatInterval;

				HasActiveRhythmEngine = process.StartTime != 0;
			});		
		}
	}

	[AlwaysUpdateSystem]
	public class PlaySongSystem : GameBaseSystem
	{
		public Dictionary<string, DescriptionFileJsonData> Files;
		public SongDescription CurrentSong;

		private AudioSource[] m_BgmSources;

		protected override void OnCreate()
		{
			AudioSource CreateAudioSource(string name, float volume)
			{
				var audioSource = new GameObject("(Clip) " + name, typeof(AudioSource)).GetComponent<AudioSource>();
				audioSource.reverbZoneMix = 0f;
				audioSource.spatialBlend  = 0f;
				audioSource.volume        = volume;
				audioSource.loop          = true;

				return audioSource;
			}

			base.OnCreate();

			Files = new Dictionary<string, DescriptionFileJsonData>();

			var songFiles = Directory.GetFiles(Application.streamingAssetsPath + "/songs", "*.json", SearchOption.TopDirectoryOnly);
			foreach (var file in songFiles)
			{
				try
				{
					var obj = JsonConvert.DeserializeObject<DescriptionFileJsonData>(File.ReadAllText(file));
					Debug.Log($"Found song: (id={obj.identifier}, name={obj.name})");

					Files[obj.identifier] = obj;

					LoadSong(obj.identifier);
				}
				catch (Exception ex)
				{
					Debug.LogError("Couldn't parse song file: " + file);
					Debug.LogException(ex);
				}
			}

			m_BgmSources = new[] {CreateAudioSource("Background Music Primary", 1), CreateAudioSource("Background Music Secondary", 1)};
		}

		private int m_CurrentBeat;
		private int m_Flip;
		private const int CmdBeats = 4;

		private AudioClip m_LastClip;

		protected override void OnUpdate()
		{
			if (CurrentSong.AreAddressableCompleted && !CurrentSong.IsFinalized)
			{
				CurrentSong.FinalizeOperation();
				Debug.Log("Finalize");
			}

			if (!CurrentSong.IsFinalized)
				return;

			var activeClientWorld = GetActiveClientWorld();
			if (activeClientWorld == null)
				return;

			var clientSystem = activeClientWorld.GetOrCreateSystem<PlaySongClientSystem>();
			if (!clientSystem.HasActiveRhythmEngine)
			{
				m_BgmSources[0].Stop();
				m_BgmSources[1].Stop();
				return;
			}

			var score       = 0;
			var isFever     = false;
			var targetAudio = default(AudioClip);
			var targetTime  = 0.0f;

			if (clientSystem.Beat >= CurrentSong.BgmEntranceClips.Count * CmdBeats)
			{
				var part          = CurrentSong.BgmComboParts[score];
				var commandLength = clientSystem.Beat != 0 ? clientSystem.Beat / CmdBeats : 0;
				targetAudio = part.Clips[commandLength % part.Clips.Count];
			}
			else
			{
				var commandLength = clientSystem.Beat != 0 ? clientSystem.Beat / CmdBeats : 0;
				targetAudio = CurrentSong.BgmEntranceClips[commandLength % CurrentSong.BgmEntranceClips.Count];
			}

			var nextBeatDelay = (((clientSystem.Beat + 1) * clientSystem.Interval) - clientSystem.Tick) * 0.001f;

			// Check if we should change clips...
			if (m_LastClip != targetAudio)
			{
				m_LastClip = targetAudio;
				if (targetAudio == null)
				{
					m_BgmSources[0].Stop();
					m_BgmSources[1].Stop();
				}
				else
				{
					m_BgmSources[1 - m_Flip].SetScheduledEndTime(AudioSettings.dspTime + nextBeatDelay);
					m_BgmSources[m_Flip].clip = m_LastClip;
					
					Debug.Log(nextBeatDelay);
					m_BgmSources[m_Flip].PlayScheduled(AudioSettings.dspTime + nextBeatDelay);
				}

				m_Flip = 1 - m_Flip;
			}
		}

		public void LoadSong(string fileId)
		{
			LoadSong(Files[fileId]);
		}

		public void LoadSong(DescriptionFileJsonData file)
		{
			if (CurrentSong != null)
			{
				CurrentSong.Dispose();
			}
			
			CurrentSong = new SongDescription(file);
		}
	}
}