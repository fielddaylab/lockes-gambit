using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using EasyAssetStreaming;
using FieldDay.Audio;
using FieldDay.Data;
using FieldDay.Files;
using FieldDay.SharedState;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FieldDay.Music {
    internal sealed class MusicPlayerState : ISharedState {
        internal RingBuffer<MusicTrack> TrackQueue;
        internal UniqueIdAllocator16 IdAllocator;

        internal StringHash32 PlaylistId;
        internal AudioHandle CurrentlyPlaying;
        internal UniqueId16 CurrentlyPlayingTrackId;
        internal bool CurrentLoop;
        internal float OverlapCountdown;

        internal MusicRepeatMode RepeatMode;
        internal MusicPlaybackState PlaybackState;

        internal MusicTransitionParams DefaultTransition;

        internal bool RegisteredSceneUnloadHandler;
        internal bool DefaultSceneUnloadStopBehavior;
        internal StringHash32 SceneUnloadContextOverridePropertyId;
    }

    public enum MusicRepeatMode : byte {
        NoRepeat,
        RepeatAll,
        RepeatSingle
    }

    public enum MusicPlaybackState : byte {
        Stopped,
        Paused,
        Playing
    }

    [Flags]
    public enum MusicTrackFlags : byte {
        Default = 0,
        PlayOnce = 0x01
    }

    public enum MusicShuffleMode : byte {
        NoShuffle,
        ShuffleAlways,
        ShuffleAfterFirstCycle,
        ShuffleAfterFirstTrack
    }

    internal struct MusicTrack : IEquatable<MusicTrack> {
        internal UniqueId16 Id;
        internal MusicTrackFlags Flags;
        internal StringHash32 EventId;
        internal MusicTransitionParams Transition;

        public bool Equals(MusicTrack other) {
            return Id == other.Id;
        }
    }

    [Serializable]
    public struct MusicTransitionParams {
        public float FadeOut;
        public float FadeIn;
        public float Overlap;
    }

    static public partial class MusicPlayer {
        /// <summary>
        /// Initializes the music player.
        /// </summary>
        static public void Initialize() {
            Assert.False(Game.SharedState.Has<MusicPlayerState>(), "MusicPlayer has already been initialized");

            EngineHints.LockHint("MUSIC_PLAYER_PLAYLIST_CAPACITY");
            int playlistCapacity = EngineHints.GetHintInt("MUSIC_PLAYER_PLAYLIST_CAPACITY", 16);

            MusicPlayerState playerState = new MusicPlayerState();
            playerState.TrackQueue = new RingBuffer<MusicTrack>(playlistCapacity, RingBufferMode.Fixed);
            playerState.IdAllocator = new UniqueIdAllocator16(playlistCapacity, false);

            Game.SharedState.Register(playerState);
            
            unsafe {
                Game.Systems.Register(&UpdateMusicPlayer,
                    new Systems.SysUpdate(GameLoopPhaseMask.PreUpdate | GameLoopPhaseMask.UnscaledLateUpdate, 10000).AllowDuringLoad(),
                    new Systems.SysPermissions()
                        .ReadWriteShared<MusicPlayerState>()
                    );
            }
        }

        static private void UpdateMusicPlayer(float dt) {
            Find.State(out MusicPlayerState music);

            if (music.PlaybackState != MusicPlaybackState.Playing || music.TrackQueue.Count == 0) {
                return;
            }

            if (music.CurrentlyPlaying.IsValid && !Sfx.IsActive(music.CurrentlyPlaying)) {
                UniqueId16 playing = music.CurrentlyPlayingTrackId;
                music.CurrentlyPlaying = default;
                music.CurrentlyPlayingTrackId = default;

                MusicTrack track = music.TrackQueue.PopFront();
                Assert.True(playing == track.Id);
                if (music.RepeatMode == MusicRepeatMode.NoRepeat || (track.Flags & MusicTrackFlags.PlayOnce) != 0) {
                    music.IdAllocator.Free(playing);
                    if (music.TrackQueue.Count == 0) {
                        Log.Msg("[MusicPlayer] Out of tracks - stopping playback");
                        music.PlaybackState = MusicPlaybackState.Stopped;
                        music.PlaylistId = default;
                    }
                } else {
                    music.TrackQueue.PushBack(track);
                }
            }

            if (music.CurrentlyPlayingTrackId == UniqueId16.Invalid) {
                if (!AudioMgr.HasFocus()) {
                    return;
                }

                if (music.OverlapCountdown > 0) {
                    if (GameLoop.IsPhase(GameLoopPhase.UnscaledLateUpdate)) {
                        music.OverlapCountdown -= dt;
                        if (music.OverlapCountdown > 0) {
                            return;
                        }
                    } else {
                        return;
                    }
                }

                Log.Msg("[MusicPlayer] Starting playback of new track");

                MusicTrack track = music.TrackQueue.PeekFront();
                music.CurrentLoop = ShouldLoopTrack(music);

                music.CurrentlyPlayingTrackId = track.Id;
                music.CurrentlyPlaying = Sfx.Play(track.EventId, new SfxPlayArgs() {
                    Delay = 0,
                    Pitch = 1,
                    Volume = 0,
                    Pan = 0
                });
                Sfx.SetVolume(music.CurrentlyPlaying, 1, track.Transition.FadeIn);
                Sfx.SetLooping(music.CurrentlyPlaying, music.CurrentLoop);

                if (!music.CurrentLoop) {
                    Sfx.QueueForUnload(music.CurrentlyPlaying);
                }

                if (music.TrackQueue.Count > 1) {
                    Game.Audio.QueuePreload(music.TrackQueue[1].EventId);
                }
            } else {
                bool shouldLoopCurrent = ShouldLoopTrack(music);
                if (music.CurrentLoop != shouldLoopCurrent) {
                    music.CurrentLoop = shouldLoopCurrent;
                    Sfx.SetLooping(music.CurrentlyPlaying, shouldLoopCurrent);
                }

                if (!shouldLoopCurrent && music.TrackQueue.Count > 1) {
                    MusicTrack nextTrack = music.TrackQueue[1];
                    if (Sfx.IsLoaded(music.CurrentlyPlaying)) {
                        AudioSource currentSource = Sfx.GetSource(music.CurrentlyPlaying);
                        float timeFromEnd = currentSource.clip.length - currentSource.time;
                        if (timeFromEnd <= nextTrack.Transition.FadeOut) {
                            Log.Msg("[MusicPlayer] Starting transition to next track");
                            Sfx.Stop(music.CurrentlyPlaying, nextTrack.Transition.FadeOut);
                            music.CurrentlyPlaying = default;
                            music.CurrentlyPlayingTrackId = default;
                            music.OverlapCountdown = nextTrack.Transition.FadeOut - nextTrack.Transition.Overlap;

                            MusicTrack prevTrack = music.TrackQueue.PopFront();
                            if (music.RepeatMode == MusicRepeatMode.NoRepeat || (prevTrack.Flags & MusicTrackFlags.PlayOnce) != 0) {
                                music.IdAllocator.Free(prevTrack.Id);
                            } else {
                                music.TrackQueue.PushBack(prevTrack);
                            }
                        }
                    }
                }
            }
        }

        static private bool ShouldLoopTrack(MusicPlayerState music) {
            return music.RepeatMode == MusicRepeatMode.RepeatSingle || (music.TrackQueue.Count == 1 && (music.TrackQueue[0].Flags & MusicTrackFlags.PlayOnce) == 0);
        }

        #region Defaults

        /// <summary>
        /// Sets the default music transition.
        /// </summary>
        static public void SetDefaultTransition(MusicTransitionParams transition) {
            Find.State<MusicPlayerState>().DefaultTransition = transition;
        }

        #endregion // Defaults

        #region Playback State

        /// <summary>
        /// Returns the current playback state.
        /// </summary>
        static public MusicPlaybackState GetPlaybackState() {
            return Find.State<MusicPlayerState>().PlaybackState;
        }

        /// <summary>
        /// Sets the music playback state. This will stop, pause, or resume playback.
        /// </summary>
        static public bool SetPlaybackState(MusicPlaybackState state) {
            Find.State(out MusicPlayerState music);

            if (music.PlaybackState != state) {
                music.PlaybackState = state;

                Log.Msg("[MusicPlayer] Setting playback state to '{0}'", state);

                switch(state) {
                    case MusicPlaybackState.Stopped: {
                        if (music.CurrentlyPlaying.IsValid) {
                            Log.Msg("[MusicPlayer] Fading out current track");
                            Sfx.Stop(music.CurrentlyPlaying, music.DefaultTransition.FadeOut);
                            music.CurrentlyPlaying = default;
                            music.CurrentlyPlayingTrackId = default;
                            MusicTrack track = music.TrackQueue.PopFront();
                            if (music.RepeatMode == MusicRepeatMode.NoRepeat || (track.Flags & MusicTrackFlags.PlayOnce) != 0) {
                                music.IdAllocator.Free(track.Id);
                            } else {
                                music.TrackQueue.PushBack(track);
                            }
                        }
                        music.OverlapCountdown = 0;
                        break;
                    }
                    case MusicPlaybackState.Paused: {
                        if (music.CurrentlyPlaying.IsValid) {
                            Sfx.SetPaused(music.CurrentlyPlaying, true);
                        }
                        break;
                    }
                    case MusicPlaybackState.Playing: {
                        if (music.CurrentlyPlaying.IsValid) {
                            Sfx.SetPaused(music.CurrentlyPlaying, false);
                        }
                        break;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Starts playing the playlist.
        /// </summary>
        static public bool Play() {
            return SetPlaybackState(MusicPlaybackState.Playing);
        }

        /// <summary>
        /// Stops playing the current track.
        /// </summary>
        static public bool Stop() {
            return SetPlaybackState(MusicPlaybackState.Stopped);
        }

        /// <summary>
        /// Stops playing the current track.
        /// </summary>
        static public bool Stop(float fadeOut) {
            Find.State(out MusicPlayerState music);

            if (music.PlaybackState != MusicPlaybackState.Stopped) {
                music.PlaybackState = MusicPlaybackState.Stopped;

                Log.Msg("[MusicPlayer] Setting playback state to 'Stopped'");

                if (music.CurrentlyPlaying.IsValid) {
                    Log.Msg("[MusicPlayer] Fading out current track");
                    Sfx.Stop(music.CurrentlyPlaying, fadeOut);
                    music.CurrentlyPlaying = default;
                    music.CurrentlyPlayingTrackId = default;
                    MusicTrack track = music.TrackQueue.PopFront();
                    if (music.RepeatMode == MusicRepeatMode.NoRepeat || (track.Flags & MusicTrackFlags.PlayOnce) != 0) {
                        music.IdAllocator.Free(track.Id);
                    } else {
                        music.TrackQueue.PushBack(track);
                    }
                }
                music.OverlapCountdown = 0;
                return true;
            }

            return false;
        }

        #endregion // Playback State

        #region Repeat Mode

        /// <summary>
        /// Retrieves the current playlist repeat mode.
        /// </summary>
        static public MusicRepeatMode GetRepeatMode() {
            return Find.State<MusicPlayerState>().RepeatMode;
        }

        /// <summary>
        /// Sets the current playlist repeat mode.
        /// </summary>
        static public void SetRepeatMode(MusicRepeatMode repeatMode) {
            Find.State<MusicPlayerState>().RepeatMode = repeatMode;
        }

        #endregion // Repeat Mode

        #region Queue

        /// <summary>
        /// Enqueues a track to be played.
        /// </summary>
        static public UniqueId16 QueueTrack(StringHash32 eventId, MusicTrackFlags flags = default) {
            Find.State(out MusicPlayerState music);
            return QueueTrackInternal(music, new MusicTrack() {
                EventId = eventId,
                Flags = flags,
                Transition = music.DefaultTransition
            });
        }

        /// <summary>
        /// Enqueues a track to be played.
        /// </summary>
        static public UniqueId16 QueueTrack(StringHash32 eventId, MusicTransitionParams transition, MusicTrackFlags flags = default) {
            Find.State(out MusicPlayerState music);
            return QueueTrackInternal(music, new MusicTrack() {
                EventId = eventId,
                Flags = flags,
                Transition = transition
            });
        }

        static private UniqueId16 QueueTrackInternal(MusicPlayerState player, MusicTrack track) {
            UniqueId16 id = player.IdAllocator.Alloc();
            track.Id = id;
            player.TrackQueue.PushBack(track);
            return id;
        }

        /// <summary>
        /// Removes a track from playback.
        /// </summary>
        static public bool DequeueTrack(UniqueId16 trackId) {
            Find.State(out MusicPlayerState music);

            if (!music.IdAllocator.Free(trackId)) {
                return false;
            }

            if (music.CurrentlyPlayingTrackId == trackId) {
                if (music.CurrentlyPlaying.IsValid) {
                    Log.Msg("[MusicPlayer] Fading out current track");
                    Sfx.Stop(music.CurrentlyPlaying, music.DefaultTransition.FadeOut);
                    music.CurrentlyPlaying = default;
                }
                music.CurrentlyPlayingTrackId = default;
                music.TrackQueue.PopFront();
                return true;
            }

            for(int i = 0; i < music.TrackQueue.Count; i++) {
                if (music.TrackQueue[i].Id == trackId) {
                    music.TrackQueue.RemoveAt(i);
                    return true;
                }
            }

            Assert.Fail("Id was valid but no tracks with id found");
            return false;
        }

        /// <summary>
        /// Stops playing the current track and clears the queue.
        /// </summary>
        static public void ClearQueue(float fadeOut = -1) {
            if (fadeOut >= 0) {
                Stop(fadeOut);
            } else {
                SetPlaybackState(MusicPlaybackState.Stopped);
            }
            
            Find.State(out MusicPlayerState music);

            while(music.TrackQueue.TryPopBack(out var track)) {
                music.IdAllocator.Free(track.Id);
            }

            music.TrackQueue.Clear();
        }

        #endregion // Queue

        #region Looping Track

        /// <summary>
        /// Sets the current looping track.
        /// </summary>
        static public bool SetLoopingTrack(StringHash32 eventId, MusicTransitionParams transition) {
            if (eventId.IsEmpty) {
                return Stop(transition.FadeOut);
            }

            Find.State(out MusicPlayerState music);
            return SetLoopingTrackInternal(music, new MusicTrack() {
                EventId = eventId,
                Transition = transition
            });
        }

        /// <summary>
        /// Sets the current looping track.
        /// </summary>
        static public bool SetLoopingTrack(StringHash32 eventId) {
            if (eventId.IsEmpty) {
                return Stop();
            }

            Find.State(out MusicPlayerState music);
            return SetLoopingTrackInternal(music, new MusicTrack() {
                EventId = eventId,
                Transition = music.DefaultTransition
            });
        }

        static private bool SetLoopingTrackInternal(MusicPlayerState player, MusicTrack track) {
            player.RepeatMode = MusicRepeatMode.RepeatSingle;
            if (player.TrackQueue.Count > 0) {
                while (player.TrackQueue.Count > 1) {
                    MusicTrack undone = player.TrackQueue.PopBack();
                    player.IdAllocator.Free(undone.Id);
                }

                MusicTrack current = player.TrackQueue[0];
                if (current.EventId == track.EventId) {
                    return false;
                }

                player.IdAllocator.Free(current.Id);
                player.TrackQueue.PopFront();
            }

            if (player.CurrentlyPlaying.IsValid) {
                Log.Msg("[MusicPlayer] Fading out current track");
                Sfx.Stop(player.CurrentlyPlaying, track.Transition.FadeOut);
                player.CurrentlyPlaying = default;
                player.CurrentlyPlayingTrackId = default;
                player.OverlapCountdown = track.Transition.FadeOut - track.Transition.Overlap;
            }

            QueueTrackInternal(player, track);

            if (player.PlaybackState == MusicPlaybackState.Stopped) {
                player.PlaybackState = MusicPlaybackState.Playing;
            }

            return true;
        }

        #endregion // Looping Track

        #region Playlist Id

        /// <summary>
        /// Returns the assigned playlist id.
        /// </summary>
        static public StringHash32 GetPlaylistId() {
            return Find.State<MusicPlayerState>().PlaylistId;
        }

        /// <summary>
        /// Sets the assigned playlist id.
        /// Returns if the playlist was switched.
        /// </summary>
        static public bool SetPlaylistId(StringHash32 id) {
            Find.State(out MusicPlayerState music);
            if (music.PlaylistId != id) {
                music.PlaylistId = id;
                Log.Msg("[MusicPlayer] Playlist id set to '{0}'", id);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the assigned playlist id.
        /// If this switches playlists, the currently queue will be cleared.
        /// </summary>
        static public bool SetPlaylistIdAndClear(StringHash32 id, float fadeOut = -1) {
            if (SetPlaylistId(id)) {
                ClearQueue(fadeOut);
                return true;
            }

            return false;
        }

        #endregion // Playlist Id

        #region Scene Unload Behavior

        /// <summary>
        /// Configures if the music queue should be cleared during a Main scene unload.
        /// </summary>
        static public void ConfigureSceneUnloadBehavior(bool clearQueue, StringHash32 overrideContextId) {
            Find.State(out MusicPlayerState music);

            if (!music.RegisteredSceneUnloadHandler) {
                Game.Scenes.OnMainSceneUnloading.Register(HandleSceneUnload);
                music.RegisteredSceneUnloadHandler = true;
            }

            music.DefaultSceneUnloadStopBehavior = clearQueue;
            music.SceneUnloadContextOverridePropertyId = overrideContextId;
        }

        static private void HandleSceneUnload() {
            Find.State(out MusicPlayerState music);
            bool clearQueue = music.DefaultSceneUnloadStopBehavior;

            if (!music.SceneUnloadContextOverridePropertyId.IsEmpty) {
                Game.Scenes.GetQueuedLoadContext(out var context);
                clearQueue = clearQueue ^ context.Get(music.SceneUnloadContextOverridePropertyId, false).AsBool();
            }

            if (clearQueue) {
                ClearQueue();
            }
        }

        #endregion // Scene Unload Behavior
    }
}