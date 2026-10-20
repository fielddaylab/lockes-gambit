using FieldDay.Localization;
using NUnit.Framework;

namespace FieldDay.Editor.UnitTests {
    static internal class LocalizationTests {
        static private void Prelude() {
            Loc.ClearSettings();
            Loc.ConfigureDefaultLanguage(Languages.English);
            Loc.SetLoadedLanguage(Languages.Spanish);
        }

        [Test]
        static public void CanLocalizePaths() {
            Prelude();

            string testPath = "en/something/en/blah.en";
            string expected = "es/something/es/blah.es";

            bool isLocalized = Loc.IsLocalizedPath(testPath);
            Assert.IsTrue(isLocalized);

            bool nonDefaultLocalized = Loc.IsLocalizedPath(expected);
            Assert.IsFalse(nonDefaultLocalized);

            string localizedTestPath = Loc.Path(testPath);
            Assert.AreEqual(expected, localizedTestPath);
        }
    }
}