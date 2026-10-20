using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Data;
using FieldDay.Music;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.SharedState;
using SpaceFab.Comic;
using SpaceFab.Design;
using SpaceFab.Save;
using System;
using System.Collections;
using UnityEngine;

namespace SpaceFab
{
    public class ChapterState : SharedStateComponent, ISaveStateChunkObject, IRegistrationCallbacks, ISceneLoadDependency
    {
        #region Saved

        [NonSerialized] public int ChapterIndex;
        [NonSerialized] public int LastSelectedContractIndex;

        #endregion // Saved

        [NonSerialized] public StringHash32 ChapterId;
        [NonSerialized] public ChapterDef ChapterDefinition;
        [NonSerialized] public UniqueId16 ChapterScriptHandle;

        [NonSerialized] public Routine LoadRoutine;

        public bool IsLoaded(SceneLoadFence fence) {
            return !LoadRoutine;
        }

        public void OnDeregister() {
            Game.Scenes.DeregisterLoadDependency(this);
        }

        public void OnRegister()
        {
            LastSelectedContractIndex = -1;
            SpacefabGame.SaveBuffer.RegisterHandler("ChapterState", this);
            Game.Scenes.RegisterLoadDependency(this);
        }


        #region Interfaces

        // ISaveStateChunkObject

        public void Read(object self, ref ByteReader reader, SaveStateChunkConsts consts)
        {
            reader.Read(ref ChapterIndex);
            reader.Read(ref LastSelectedContractIndex);
        }

        public void Write(object self, ref ByteWriter writer, SaveStateChunkConsts consts)
        {
            writer.Write(ChapterIndex);
            writer.Write(LastSelectedContractIndex);
        }

        #endregion // Interfaces
    }

    public static partial class ChapterUtility {
        #region Data Load/Unload

        static public bool LoadChapterData(ChapterState chapterState, int chapterIndex) {
            return LoadChapterData(chapterState, GetLoadInfo(chapterIndex));
        }

        static public bool LoadChapterData(ChapterState chapterState, StringHash32 chapterId) {
            return LoadChapterData(chapterState, GetLoadInfo(chapterId));
        }

        static public bool LoadChapterData(ChapterState chapterState, ChapterManifest.Entry loadInfo) {
            if (chapterState.ChapterId == loadInfo.ChapterId) {
                HandleMusicPlayer();
                return false;
            }

            UnloadChapterData(chapterState);
            chapterState.ChapterId = loadInfo.ChapterId;
            chapterState.LoadRoutine.Replace(chapterState, LoadChapterProcess(chapterState, loadInfo));
            return true;
        }

        static private IEnumerator LoadChapterProcess(ChapterState chapterState, ChapterManifest.Entry loadInfo) {
            Game.Assets.LoadStreamedPackage(loadInfo.PackageId);
            while (Game.Assets.IsLoadingStreamedPackage(loadInfo.PackageId)) {
                yield return null;
            }

            ChapterDef chapterAsset = Find.NamedAsset<ChapterDef>(loadInfo.ChapterId);
            chapterState.ChapterDefinition = chapterAsset;
            chapterState.ChapterScriptHandle = ScriptDBUtility.Load(chapterAsset.Script);
            Game.Events.Queue(GameEvents.ChapterLoaded);

            if (Game.Scenes.IsMainLoaded()) {
                HandleMusicPlayer();
            } else {
                Game.Scenes.QueueOnLoad(HandleMusicPlayer);
            }
        }

        static public bool UnloadChapterData(ChapterState chapterState) {
            if (chapterState.ChapterId.IsEmpty) {
                return false;
            }

            ScriptDBUtility.Unload(chapterState.ChapterScriptHandle);
            MusicPlayer.ClearQueue(0);
            MusicPlayer.SetPlaylistId(null);

            var loadInfo = GetLoadInfo(chapterState.ChapterId);
            Game.Assets.UnloadStreamedPackage(loadInfo.PackageId);

            chapterState.LoadRoutine.Stop();
            chapterState.ChapterDefinition = null;
            chapterState.ChapterScriptHandle = default;
            chapterState.ChapterId = default;
            Game.Events.Queue(GameEvents.ChapterUnloaded);
            return true;
        }

        #endregion // Data Load/Unload

        #region Music Playback

        static private void HandleMusicPlayer() {
            Find.State(out ChapterState chapterState);
            if (chapterState.ChapterDefinition) {
                MusicPlayer.SetPlaylistFromAssetId(chapterState.ChapterDefinition.MusicPlaylist);
            }
        }

        #endregion // Music Playback

        static public StringHash32 GetSelectedContractId(ChapterState chapterState) {
            if (chapterState.LastSelectedContractIndex < 0 || !chapterState.ChapterDefinition) {
                return null;
            }

            return chapterState.ChapterDefinition.AvailableContracts[chapterState.LastSelectedContractIndex];
        }

        static public bool LoadCurrentChapter(ChapterState chapterState) {
            return LoadChapterData(chapterState, chapterState.ChapterIndex);
        }

        static public float GetGlitchChance(ChapterState chapterState)
        {
            ChapterDef def = chapterState.ChapterDefinition;
            int index = chapterState.LastSelectedContractIndex;
            if (def == null || index < 0 || index >= def.GlitchChances.Length)
            {
                return 0f;
            }
            return def.GlitchChances[index];
        }

        public static void LoadNextChapter(ChapterState chapterState, PlayerProgressState progressState, ContractState contractState, MinigameSaveStates saveStates)
        {
            // save elapsed cycles and funds
            progressState.ElapsedCycles += saveStates.Fabrication.FinalizedTotalCycles;
            progressState.ElapsedCycles += saveStates.Supply.FinalizedTotalCycles;

            int contractPayout = 0;
            if (contractState.ContractDefinition) {
                contractPayout = contractState.ContractDefinition.Payout();
            }
            progressState.Funds += contractPayout - saveStates.Supply.FinalizedCost;

            // advance chapter
            chapterState.ChapterIndex++;

            Find.GlobalAsset(out ChapterManifest chapterManifest);

            ContractUtility.UnloadContractData(contractState);

            if (chapterState.ChapterIndex >= chapterManifest.Chapters.Length) {
                ComicScripting.LoadComic(chapterManifest.EpilogueComicPackage);
                Game.Scenes.GetQueuedLoadContext(out SceneRequestContext context);
                context.Set("QueueCredits", true);
                context.Set("UnloadChapter", true);
                Game.Scenes.QueueMainLoadContext(context);
            } else {
                SaveUtility.Save(SaveSlot.Main);
                ComicScripting.LoadComicForCurrentChapter();
            }
        }
    }
}