using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Patapon4TLB.UI.GameMode.VSHeadOn
{
	public class UiHeadOnScoreFrame : MonoBehaviour
	{
		[Serializable]
		public struct Category
		{
			public GameObject      SubFrame;
			public TextMeshProUGUI Label;
			public MaskableGraphic Image;

			internal int PreviousScore;
		}

		public Category[] Categories;
		
		private void OnEnable()
		{
			for (var i = 0; i != Categories.Length; i++)
			{
				ref var cat = ref Categories[i];
				cat.PreviousScore = -999;
			}
		}

		public void SetScore(int category, int score)
		{
			ref var cat = ref Categories[category];
			if (cat.PreviousScore == score)
				return;

			cat.PreviousScore = score;
			cat.Label.text    = score.ToString();
		}
	}
}