using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Patapon4TLB.Core
{
	public class UnitVisualPresentation : RuntimeAssetPresentation<UnitVisualPresentation>
	{
		public Animator Animator;

		public void Update()
		{
			if (Input.GetKeyDown(KeyCode.Keypad4))
				Animator.SetTrigger("Pata");
		}
	}

	public class UnitVisualBackend : RuntimeAssetBackend<UnitVisualPresentation>
	{
		protected override void Update()
		{
			if (DstEntityManager == null || DstEntityManager.IsCreated && DstEntityManager.Exists(DstEntity))
			{
				base.Update();
				return;
			}
			
			DisableNextUpdate                 = true;
			ReturnToPoolOnDisable             = true;
			ReturnPresentationToPoolNextFrame = true;
			
			base.Update();
		}
	}

	[UpdateInGroup(typeof(ClientPresentationSystemGroup))]
	[UpdateAfter(typeof(RenderInterpolationSystem))]
	public class UpdateBackend : ComponentSystem
	{
		protected override void OnUpdate()
		{
			EntityManager.CompleteAllJobs();
			
			Entities.ForEach((Transform transform, UnitVisualBackend backend) =>
			{				
				transform.position = EntityManager.GetComponentData<Translation>(backend.DstEntity).Value;
			});
		}
	}
}