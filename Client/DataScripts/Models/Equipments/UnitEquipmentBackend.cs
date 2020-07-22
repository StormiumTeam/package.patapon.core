using StormiumTeam.GameBase.Utility.AssetBackend;
using UnityEngine;

namespace PataNext.Client.DataScripts.Models.Equipments
{
	public class UnitEquipmentBackend : RuntimeAssetBackend<BaseUnitEquipmentPresentation>
	{
		public override bool PresentationWorldTransformStayOnSpawn => false;

		public override void OnPresentationSet()
		{
			base.OnPresentationSet();
			
			foreach (var tr in gameObject.GetComponentsInChildren<Transform>())
				tr.gameObject.layer = gameObject.layer;
		}
	}

	public abstract class BaseUnitEquipmentPresentation : RuntimeAssetPresentation<BaseUnitEquipmentPresentation>
	{
	}
}