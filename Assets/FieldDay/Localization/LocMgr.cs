using BeauUtil.Debugger;

namespace FieldDay.Localization {
    public sealed class LocMgr {
        #region State

        private LocDb m_MainDb;
        private LocDb m_SubDb;

        #endregion // State

        #region Events

        internal void Initialize(LanguageId defaultLanguage) {
            Loc.ConfigureDefaultLanguage(defaultLanguage);
            Loc.SetLoadedLanguage(defaultLanguage);

            m_MainDb = new LocDb(16);
            m_SubDb = new LocDb(16);
        }

        internal void Shutdown() {
            m_MainDb.Clear();
            m_SubDb.Clear();

            m_MainDb = null;
            m_SubDb = null;
        }

        #endregion // Events
    }
}