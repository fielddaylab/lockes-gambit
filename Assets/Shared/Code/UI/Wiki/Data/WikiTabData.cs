using FieldDay.Assets;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaceFab.UI {
    /// <summary>
    /// ScriptableObject authored per wiki section/tab. Identity (used for external OpenTo(tabId)
    /// calls) is the asset's name via NamedAsset.AssetId. Carries an ordered list of pages; a
    /// tab is considered "unlocked" iff at least one of its pages is unlocked in PlayerProgress.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceFab/Wiki/Tab")]
    public class WikiTabData : NamedAsset {
        [SerializeField, FormerlySerializedAs("m_Title")] public string Title;
        [SerializeField, FormerlySerializedAs("m_Icon")] public Sprite Icon;
        [SerializeField, FormerlySerializedAs("m_Pages")] public WikiPageData[] Pages;
    }
}
