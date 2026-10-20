using FieldDay.Assets;
using FieldDay.Data;
using UnityEngine;

namespace FieldDay.Localization {
    /// <summary>
    /// Localization configuration.
    /// </summary>
    public sealed class LocManifest : NamedAsset, IEditorOnlyData {
        [SerializeField] private LanguageId m_Language;
        [SerializeField] private string m_BinaryPath;
        [SerializeField] private TextAsset[] m_CsvFiles;

        /// <summary>
        /// This file's language.
        /// </summary>
        public LanguageId Language {
            get { return m_Language; }
        }

#if UNITY_EDITOR

        void IEditorOnlyData.ClearEditorData(bool isDevelopmentBuild) {
            m_CsvFiles = null;
        }

#endif // UNITY_EDITOR
    }
}