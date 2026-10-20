using FieldDay.Components;
using SpaceFab.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Fabrication
{
    public class StationResultDisplay : BatchedComponent
    {
        public Image[] Rating;

        public void SetRating(float precision, ResultDisplayConfig config)
        {
            int rating = (int)(precision * 10f / 3f);
            for (int i = 0; i < Rating.Length; i++)
                Rating[i].sprite = rating > i ? config.StationRatingFilled : config.StationRatingEmpty;
        }
    }
}