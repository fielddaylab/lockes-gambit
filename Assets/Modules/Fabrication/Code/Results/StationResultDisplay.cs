using FieldDay.Components;
using SpaceFab.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Fabrication
{
    public class StationResultDisplay : BatchedComponent
    {
        public Image[] Rating;
        public Sprite GoodStar;
        public Sprite EmptyStar;

        public void SetRating(float precision)
        {
            int rating = (int)(precision * 10f / 3f);
            for (int i = 0; i < Rating.Length; i++)
                Rating[i].sprite = rating > i ? GoodStar : EmptyStar;
        }
    }
}