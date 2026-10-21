using BeauUtil;
using FieldDay.Assets;
using FieldDay.Audio;
using System;
using UnityEngine;

namespace FieldDay.Music {
    /// <summary>
    /// Music playlist and mode configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "Field Day/Music/Music Asset")]
    public sealed class MusicAsset : NamedAsset {
        [AudioEvent, Tooltip("If set, this track will only play once.")] public StringHash32 IntroTrackId;
        [AudioEvent] public StringHash32[] FullTracks = Array.Empty<StringHash32>();

        [Header("Configuration")]
        public bool SeamlessIntroTransition;
        public MusicTransitionParams Transition;
        public MusicRepeatMode Repeat = MusicRepeatMode.RepeatAll;
    }

    static public partial class MusicPlayer {
        /// <summary>
        /// Sets the current playlist from a music asset.
        /// </summary>
        static public bool SetPlaylistFromAsset(MusicAsset asset, float fadeOut = -1) {
            if (asset == null) {
                return SetPlaylistIdAndClear(null, fadeOut);
            }

            if (SetPlaylistIdAndClear(asset.AssetId, fadeOut)) {
                SetRepeatMode(asset.Repeat);

                if (!asset.IntroTrackId.IsEmpty) {
                    QueueTrack(asset.IntroTrackId, MusicTrackFlags.PlayOnce);
                }

                for (int i = 0; i < asset.FullTracks.Length; i++) {
                    QueueTrack(asset.FullTracks[i], i > 0 || !asset.SeamlessIntroTransition ? asset.Transition : default);
                }

                Play();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the current playlist from a music asset.
        /// </summary>
        static public bool QueuePlaylistFromAsset(MusicAsset asset) {
            if (asset == null) {
                return SetPlaylistIdAndClear(null, -1);
            }

            if (SetPlaylistId(asset.AssetId)) {
                ClearFutureQueue();
                SetRepeatMode(asset.Repeat);

                if (!asset.IntroTrackId.IsEmpty) {
                    QueueTrack(asset.IntroTrackId, MusicTrackFlags.PlayOnce);
                }

                for (int i = 0; i < asset.FullTracks.Length; i++) {
                    QueueTrack(asset.FullTracks[i], i > 0 || !asset.SeamlessIntroTransition ? asset.Transition : default);
                }

                Play();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the current playlist from a music asset id.
        /// </summary>
        static public bool SetPlaylistFromAssetId(StringHash32 assetId, float fadeOut = -1) {
            if (assetId.IsEmpty) {
                return SetPlaylistIdAndClear(null, fadeOut);
            }

            return SetPlaylistFromAsset(Find.NamedAsset<MusicAsset>(assetId), fadeOut);
        }

        /// <summary>
        /// Queues a playlist from a music asset id.
        /// </summary>
        static public bool QueuePlaylistFromAssetId(StringHash32 assetId) {
            if (assetId.IsEmpty) {
                return QueuePlaylistFromAsset(null);
            }

            return QueuePlaylistFromAsset(Find.NamedAsset<MusicAsset>(assetId));
        }
    }
}