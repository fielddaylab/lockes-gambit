using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Components;
using FieldDay.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaceFab.Overarching
{
    public class MinigameZone : BatchedComponent, IRegistrationCallbacks
    {
        // Identifies which minigame this zone represents. Drives the per-zone alert mask lookup
        // in OverarchingAlertState and the FoundValidSolution auto-rule.
        public MinigameId Minigame;
        public SceneReference Scene;

        [Header("Components")]
        [Required] public CursorHint Cursor;
        [Required] public MinigameZoneOverlay Overlay;

        [NonSerialized] public MinigameZoneStatus CachedStatus;
        [NonSerialized] public int CursorCount = 0;

        public void OnRegister() {
            Cursor.onClick.AddListener(HandleClick);
            Cursor.onPointerEnter.AddListener(HandlePointerEnter);
            Cursor.onPointerExit.AddListener(HandlePointerExit);

            Overlay.AlternateCursor.onClick.AddListener(HandleClick);
            Overlay.AlternateCursor.onPointerEnter.AddListener(HandlePointerEnter);
            Overlay.AlternateCursor.onPointerExit.AddListener(HandlePointerExit);

            CursorHint.CopyTooltips(Cursor, Overlay.AlternateCursor);

            Cursor.enabled = false;
        }

        public void OnDeregister() {
        }

        #region Pointer Handlers

        private void HandleClick()
        {
            MinigameZonesUtility.OnClick(this);
        }

        private void HandlePointerEnter()
        {
            if (CursorCount++ == 0) {
                MinigameZonesUtility.SetHoverState(this, true);
                Sfx.Play("Overarching.Zone.Hover");
            }
        }

        private void HandlePointerExit()
        {
            if (CursorCount-- == 1) {
                MinigameZonesUtility.SetHoverState(this, false);
            }
        }

        #endregion // Pointer Handlers
    }

    public enum MinigameZoneStatus {
        Disabled,
        Locked,
        NotStarted,
        InProgress,
        Completed
    }

    public static partial class MinigameZonesUtility
    {
        public static void OnClick(MinigameZone zone) {
            Find.State<MinigameZonesState>().QueuedZone = zone;
        }

        static public void UpdateZoneStatus(MinigameZone zone, MinigameZoneStatus status) {
            zone.CachedStatus = status;

            zone.Cursor.enabled = status > MinigameZoneStatus.Locked;
            zone.Overlay.AlternateCursor.GetComponent<Collider>().enabled = status < MinigameZoneStatus.Completed;

            zone.Overlay.CompletedBadge.gameObject.SetActive(status == MinigameZoneStatus.Completed);
            zone.Overlay.HighlightFill.enabled = zone.Overlay.HighlightOutline.enabled = status > MinigameZoneStatus.Locked;

            zone.Overlay.NameBadge.SetActive(zone.CursorCount > 0 | status == MinigameZoneStatus.NotStarted | status == MinigameZoneStatus.InProgress);
            zone.Overlay.HighlightFill.color = status == MinigameZoneStatus.Completed ? zone.Overlay.NeutralColor : zone.Overlay.ThemeColor;
        }

        static public void SetHoverState(MinigameZone zone, bool hoverActive) {
            Find.State(out MinigameZonesState state);

            zone.Overlay.HighlightOutline.color = hoverActive ? Color.white : Color.black;
            zone.Overlay.HighlightOutline.sortingOrder = hoverActive ? -2 : -4;
            zone.Overlay.NameFill.color = hoverActive ? zone.Overlay.ThemeColor : Color.black;
            zone.Overlay.NameBadge.SetActive(hoverActive | zone.CachedStatus == MinigameZoneStatus.NotStarted | zone.CachedStatus == MinigameZoneStatus.InProgress);

            if (hoverActive) {
                if (state.HoverZone != zone) {
                    state.HoverZone = zone;
                }
            } else {
                if (state.HoverZone == zone) {
                    state.HoverZone = null;
                }
            }
        }
    }
}