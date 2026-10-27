using BeauUtil;
using System;
using System.Diagnostics;

#if FIELD_DAY_INCLUDE_SCRIPTING
using Leaf.Runtime;
#endif // FIELD_DAY_INCLUDE_SCRIPTING

namespace FieldDay.Music {
    static public class MusicScripting {
#if !FIELD_DAY_INCLUDE_SCRIPTING
        [Conditional("__STUB")]
        private sealed class LeafMemberAttribute : Attribute {
            public LeafMemberAttribute(string name) { }
        }
#endif // #!FIELD_DAY_INCLUDE_SCRIPTING

        [LeafMember("MusicLoop")]
        static public void MusicLoop(StringHash32 musicId) {
            if (musicId.IsEmpty) {
                MusicPlayer.SetPlaylistId(null);
                MusicPlayer.ClearQueueAndStop();
            } else if (Game.Assets.TryGetNamed(musicId, out MusicAsset playlist)) {
                MusicPlayer.SetPlaylistFromAsset(playlist);
                MusicPlayer.SetRepeatMode(MusicRepeatMode.RepeatAll);
            } else {
                MusicPlayer.SetPlaylistId(null);
                MusicPlayer.SetLoopingTrack(musicId);
            }
        }

        [LeafMember("MusicStop")]
        static public void MusicStop(float fadeOut = -1) {
            MusicPlayer.ClearQueueAndStop(fadeOut);
        }

        [LeafMember("MusicPreload")]
        static public void MusicPreload(StringHash32 musicId) {
            if (musicId.IsEmpty) {
                return;
            }
            if (Game.Assets.TryGetNamed(musicId, out MusicAsset playlist)) {
                if (!playlist.IntroTrackId.IsEmpty) {
                    Game.Audio.QueuePreload(playlist.IntroTrackId);
                }

                foreach(var track in playlist.FullTracks) {
                    Game.Audio.QueuePreload(track);
                }
            } else {
                Game.Audio.QueuePreload(musicId);
            }
        }
    }
}