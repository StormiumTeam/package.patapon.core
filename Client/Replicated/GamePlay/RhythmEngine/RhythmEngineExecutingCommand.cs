﻿using GameHost.Simulation.Features.ShareWorldState.BaseSystems;
 using GameHost.Simulation.Utility.Resource;
 using PataNext.Module.Simulation.Resources;
 using Unity.Entities;
 using Unity.Mathematics;

 namespace PataNext.Module.Simulation.Components.GamePlay.RhythmEngine
 {
	 public struct RhythmEngineExecutingCommand : IComponentData
	 {
		 public GameResource<RhythmCommandResource> Previous;
		 public GameResource<RhythmCommandResource> CommandTarget;

		 /// <summary>
		 /// At which 'activation' beat will the command start?
		 /// </summary>
		 public int ActivationBeatStart;

		 /// <summary>
		 /// At which 'activation' beat will the command end?
		 /// </summary>
		 public int ActivationBeatEnd;

		 /// <summary>
		 ///     Power is associated with beat score, this is a value between 0 and 100.
		 /// </summary>
		 /// <remarks>
		 ///     This is not associated at all with fever state, the command will check if there is fever or not on the engine.
		 ///     The game will check if it can enable hero mode if power is 100.
		 /// </remarks>
		 public int PowerInteger;

		 public bool WaitingForApply;

		 /// <summary>
		 /// Return a power between a range of [0..1]
		 /// </summary>
		 public double Power
		 {
			 get => PowerInteger * 0.01;
			 set => PowerInteger = (int) math.clamp(value * 100, 0, 100);
		 }

		 public override string ToString()
		 {
			 return $"Target={CommandTarget}, ActiveAt={ActivationBeatStart}, Power={Power:0.00%}";
		 }

		 public class Register : RegisterGameHostComponentData<RhythmEngineExecutingCommand>
		 {
		 }
	 }
 }