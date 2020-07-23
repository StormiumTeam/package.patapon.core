﻿﻿using System;
using GameHost.Native;
using GameHost.Simulation.Features.ShareWorldState.BaseSystems;
using GameHost.Simulation.Utility.Resource.Components;

namespace PataNext.Module.Simulation.Resources.Keys
{
	public readonly struct EquipmentResourceKey : IGameResourceKeyDescription, IEquatable<EquipmentResourceKey>
	{
		public readonly CharBuffer64 Value;

		public class Register : RegisterGameHostComponentData<GameResourceKey<EquipmentResourceKey>>
		{
		}

		public bool Equals(EquipmentResourceKey other)
		{
			return Value.Equals(other.Value);
		}

		public override bool Equals(object obj)
		{
			return obj is EquipmentResourceKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}
}