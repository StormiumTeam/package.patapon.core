﻿﻿using System;
using GameHost.Simulation.Features.ShareWorldState.BaseSystems;
 using Unity.Entities;

 namespace PataNext.Module.Simulation.Components.GamePlay.RhythmEngine
{
	public struct RhythmEngineSettings : IComponentData
	{
		public TimeSpan BeatInterval;
		public int      MaxBeat;

		public class Register : RegisterGameHostComponentData<RhythmEngineSettings>
		{
		}
	}
}