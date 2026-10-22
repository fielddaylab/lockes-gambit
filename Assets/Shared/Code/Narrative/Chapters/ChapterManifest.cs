using System;
using System.Collections.Generic;
using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Assets;
using ScriptableBake;
using SpaceFab.Materials;
using UnityEngine;

namespace SpaceFab {
    [CreateAssetMenu(menuName = "SpaceFab/Chapters/Chapter Manifest")]
    public sealed class ChapterManifest : GlobalAsset, IBaked {
        [Serializable]
        public struct Entry {
            [AssetName(typeof(ChapterDef))] public StringHash32 ChapterId;
            [StreamedPackId] public StringHash32 PackageId;
        }

        [Serializable]
        public struct ExportedData {
            [AssetName(typeof(MaterialAsset))] public StringHash32[] ResearchableMaterials;
            [StreamedPackId] public StringHash32 ComicPackage;
        }

        public Entry[] Chapters;
        public ExportedData[] ResidentData;

        [Header("Hazards")]
        [Range(0, 100)] public int[] HazardChances = new int[9];

        [Header("Epilogue")]
        [StreamedPackId("*Comic*")] public StringHash32 EpilogueComicPackage;

#if UNITY_EDITOR

        int IBaked.Order { get { return 0; } }

        unsafe bool IBaked.Bake(BakeFlags flags, BakeContext context) {
            ResidentData = new ExportedData[Chapters.Length];

            foreach(var chapterDef in Baking.FindAssets<ChapterDef>()) {
                var _ = chapterDef.AssetId;
            }

            using (PooledList<StringHash32> materialIds = PooledList<StringHash32>.Create()) {
                for (int i = 0; i < Chapters.Length; i++) {

                    ChapterDef chapter = Baking.FindAsset<ChapterDef>(Chapters[i].ChapterId);
                    Assert.NotNullOrDestroyed(chapter);

                    materialIds.AddRange(chapter.AvailableMaterials);
                    foreach(var exclude in chapter.ExcludeFromResearch) {
                        materialIds.FastRemove(exclude);
                    }

                    ResidentData[i] = new ExportedData() {
                        ResearchableMaterials = materialIds.ToArray(),
                        ComicPackage = chapter.ComicPackage
                    };
                    materialIds.Clear();
                }
            }

            return true;
        }

#endif // UNITY_EDITOR
    }

    static public partial class ChapterUtility {
        static public int GetIndex(StringHash32 chapterId) {
            Find.GlobalAsset(out ChapterManifest manifest);
            for(int i = 0, len = manifest.Chapters.Length; i < len; i++) {
                if (manifest.Chapters[i].ChapterId == chapterId) {
                    return i;
                }
            }
            Assert.Fail("No chapter with id '{0}'", chapterId);
            return -1;
        }

        static public ChapterManifest.Entry GetLoadInfo(StringHash32 chapterId) {
            Find.GlobalAsset(out ChapterManifest manifest);
            for (int i = 0, len = manifest.Chapters.Length; i < len; i++) {
                if (manifest.Chapters[i].ChapterId == chapterId) {
                    return manifest.Chapters[i];
                }
            }
            Assert.Fail("No chapter with id '{0}'", chapterId);
            return default;
        }

        static public ChapterManifest.Entry GetLoadInfo(int chapterIndex) {
            Find.GlobalAsset(out ChapterManifest manifest);
            Assert.True(chapterIndex >= 0 && chapterIndex < manifest.Chapters.Length, "Chapter index {0} out of range", chapterIndex);
            return manifest.Chapters[chapterIndex];
        }

        static public int ChapterCount() {
            Find.GlobalAsset(out ChapterManifest manifest);
            return manifest.Chapters.Length;
        }
    }
}