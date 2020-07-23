using GameBase.Roles.Components;
using GameBase.Roles.Interfaces;
using GameHost.Simulation.Features.ShareWorldState.BaseSystems;
using PataNext.Module.Simulation.Components.Roles;
using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(Relative<ProjectileDescription>))]

namespace PataNext.Module.Simulation.Components.Roles
{
	public struct ProjectileDescription : IEntityDescription
	{
		public class RegisterRelative : Relative<ProjectileDescription>.Register
		{
		}

		public class Register : RegisterGameHostComponentData<ProjectileDescription>
		{
		}
	}
}