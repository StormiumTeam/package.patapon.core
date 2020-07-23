﻿using GameHost.Simulation.Features.ShareWorldState.BaseSystems;
using Unity.Entities;

namespace PataNext.Module.Simulation.Components.Units
{
	public struct UnitDirection : IComponentData
	{
		public sbyte Value;

		public static readonly UnitDirection Left  = new UnitDirection {Value = -1};
		public static readonly UnitDirection Right = new UnitDirection {Value = +1};

		public bool IsLeft  => Value == -1;
		public bool IsRight => Value == 1;

		public bool Invalid => !IsLeft && !IsRight;

		public class Register : RegisterGameHostComponentData<UnitDirection>
		{
		}
	}
}