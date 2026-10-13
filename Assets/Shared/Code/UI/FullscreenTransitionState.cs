using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Debugging;
using FieldDay.Rendering;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using SpaceFab.Fabrication.StationControl;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceFab
{
    public sealed class FullscreenTransitionState : SharedStateComponent, IRegistrationCallbacks, ICameraPreCullCallback, IOnGuiUpdate
    {
        public enum Mode {
            Off,
            Fullscreen,

            Halftone,
            Fade,
            Subtract,
            Dither
        }

        public enum HalftoneAxis {
            Horizontal,
            Vertical,
        }

        [Header("Components and Materials")]
        public MeshRenderer Renderer;
        public Material HalftoneTransitionMaterial;
        public Material ColorTransitionMaterial;
        public Material SubtractTransitionMaterial;
        public Material DitherTransitionMaterial;

        [Header("Config")]
        public Mode DefaultMode = Mode.Halftone;
        public float DefaultTransitionTime = 0.3f;
        public Color32 DefaultColor = Color.black;
        [AudioMixStateRef] public StringHash32 TransitionMix;

        [Header("Halftone")]
        public HalftoneAxis HalftoneDirection;
        [Range(0, 1)] public float MaxHalftoneTilt = 0.4f;
        [Range(0, 1)] public float HalftoneMoveDistance = 0.5f;

        [Header("-- DEBUG --")]
        public bool ActiveState;
        public Mode CurrentMode;
        [NonSerialized] public Mode HoldMode;
        [NonSerialized] public Vector2 HalftoneNormal;

        [Range(0, 1)] public float CurrentTransitionFactor;
        [NonSerialized] public float CurrentTransitionSpeed;
        [NonSerialized] public float CurrentTransitionDelay;
        [NonSerialized] public Color32 CurrentColor;

        public void OnDeregister() {
            CameraHelper.RemoveOnPreCull(this);
            Game.Gui.DeregisterUpdate(this);
        }

#if UNITY_EDITOR
        private void OnDestroy() {
            HalftoneTransitionMaterial.DisableKeyword("INVERT_TRANSITION");
            HalftoneTransitionMaterial.SetVector("_Plane0", default);
            HalftoneTransitionMaterial.SetVector("_Plane1", default);
            HalftoneTransitionMaterial.SetColor(DefaultShaderProps.Color, default);
            ColorTransitionMaterial.SetColor(DefaultShaderProps.Color, default);
            SubtractTransitionMaterial.SetColor(DefaultShaderProps.Color, default);
            DitherTransitionMaterial.SetColor(DefaultShaderProps.Color, default);
        }
#endif // UNITY_EDITOR

        public void OnRegister()
        {
            CameraHelper.AddOnPreCull(this);
            Game.Gui.RegisterUpdate(this);
            FullscreenTransitionUtility.RegisterSceneHandlers();
            
            Renderer.enabled = false;
            CurrentTransitionFactor = 0;
            CurrentColor = DefaultColor;
            CurrentTransitionSpeed = 1 / DefaultTransitionTime;
            ActiveState = false;
            CurrentMode = Mode.Off;
        }

        void ICameraPreCullCallback.OnCameraPreCull(Camera inCamera, CameraCallbackSource inSource) {
            if (CurrentMode == Mode.Off || CurrentMode == Mode.Fullscreen || !CameraUtility.IsOverlayCamera(inCamera)) {
                return;
            }

            switch(CurrentMode) {
                case Mode.Halftone: {
                    UpdateHalftoneMaterial(inCamera);
                    break;
                }
                case Mode.Dither: {
                    UpdateMaterialAlpha(DitherTransitionMaterial);
                    break;
                }
                case Mode.Fade: {
                    UpdateMaterialAlpha(ColorTransitionMaterial);
                    break;
                }
                case Mode.Subtract: {
                    UpdateSubtractMaterial();
                    break;
                }
            }
            
        }

        private void UpdateHalftoneMaterial(Camera camera) {
            Vector2 screenSize = new Vector2(camera.pixelWidth, camera.pixelHeight);
            float tileSize = HalftoneTransitionMaterial.GetFloat("_Tiling");
            Vector2 tileSizeVec = new Vector2(tileSize, tileSize);
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            Vector4 minPlane, maxPlane;
            minPlane.z = HalftoneNormal.x;
            minPlane.w = HalftoneNormal.y;
            maxPlane.z = -HalftoneNormal.x;
            maxPlane.w = -HalftoneNormal.y;

            float moveDistance = HalftoneMoveDistance;

            Vector2 minVP, maxVP;
            if (HalftoneDirection == HalftoneAxis.Horizontal) {
                minVP = new Vector2(moveDistance * CurrentTransitionFactor, 0.5f);
                maxVP = new Vector2(1 - moveDistance * CurrentTransitionFactor, 0.5f);
            } else {
                minVP = new Vector2(0.5f, moveDistance * CurrentTransitionFactor);
                maxVP = new Vector2(0.5f, 1 - moveDistance * CurrentTransitionFactor);
            }

            Vector2 minTilePos = ShaderMath.ComputePixelTiledTexCoords(minVP, tileSizeVec, pivot, screenSize);
            Vector2 maxTilePos = ShaderMath.ComputePixelTiledTexCoords(maxVP, tileSizeVec, pivot, screenSize);

            minPlane.x = minTilePos.x;
            minPlane.y = minTilePos.y;
            maxPlane.x = maxTilePos.x;
            maxPlane.y = maxTilePos.y;

            if (ActiveState) {
                HalftoneTransitionMaterial.DisableKeyword("INVERT_TRANSITION");
            } else {
                HalftoneTransitionMaterial.EnableKeyword("INVERT_TRANSITION");
            }

            HalftoneTransitionMaterial.SetVector("_Plane0", minPlane);
            HalftoneTransitionMaterial.SetVector("_Plane1", maxPlane);
            UpdateMaterialAlpha(HalftoneTransitionMaterial);
        }

        private void UpdateSubtractMaterial() {
            Color invCurrent = CurrentColor.Invert();
            Color c = Color.LerpUnclamped(invCurrent, Color.white, CurrentTransitionFactor);
            c.a = CurrentTransitionFactor;
            SubtractTransitionMaterial.SetColor(DefaultShaderProps.Color, c);
            Renderer.sharedMaterial = SubtractTransitionMaterial;
        }

        private void UpdateMaterialAlpha(Material material) {
            Color c = CurrentColor;
            c.a = CurrentTransitionFactor;
            material.SetColor(DefaultShaderProps.Color, c);
            Renderer.sharedMaterial = material;
        }

        void IOnGuiUpdate.OnGuiUpdate() {
            Sfx.SetMixState(TransitionMix, CurrentTransitionFactor, 0);

            if (CurrentTransitionDelay > 0) {
                CurrentTransitionDelay -= Frame.DeltaTime;
                return;
            }

            if (ActiveState && CurrentMode != Mode.Fullscreen) {
                CurrentTransitionFactor += CurrentTransitionSpeed * Frame.DeltaTime;
                if (CurrentTransitionFactor >= 1) {
                    HoldMode = CurrentMode;
                    CurrentMode = Mode.Fullscreen;
                    CurrentTransitionFactor = 1;
                    UpdateMaterialAlpha(ColorTransitionMaterial);
                }
            } else if (!ActiveState && CurrentMode != Mode.Off) {
                CurrentTransitionFactor -= CurrentTransitionSpeed * Frame.DeltaTime;
                if (CurrentTransitionFactor <= 0) {
                    CurrentMode = HoldMode = Mode.Off;
                    CurrentTransitionFactor = 0;
                    Renderer.enabled = false;
                }
            }
        }
    }

    static public class FullscreenTransitionUtility {
        static public void RegisterSceneHandlers() {
            Game.Scenes.RegisterTransitionHandlers(HandleSceneUnload, HandleSceneLoaded, HandleScenePreReady);
        }
        
        static private IEnumerator HandleSceneUnload(Scene scene, StringHash32 tag, MainSceneTransitionParameters transitionArgs) {
            Game.Input.PauseAll();

            Find.State(out FullscreenTransitionState fullscreen);
            FullscreenTransitionState.Mode mode = fullscreen.DefaultMode;
            if (transitionArgs.TransitionType == "dither") {
                mode = FullscreenTransitionState.Mode.Dither;
            } else if (transitionArgs.TransitionType == "subtract") {
                mode = FullscreenTransitionState.Mode.Subtract;
            } else if (transitionArgs.TransitionType == "fade") {
                mode = FullscreenTransitionState.Mode.Fade;
            } else if (transitionArgs.TransitionType == "halftone") {
                mode = FullscreenTransitionState.Mode.Halftone;
            }

            if (transitionArgs.TransitionColorOverride.a > 0) {
                SetColor(fullscreen, transitionArgs.TransitionColorOverride);
            } else {
                ResetColor(fullscreen);
            }

            FadeOut(fullscreen, mode, fullscreen.DefaultTransitionTime, FullscreenTransitionFlags.FlipHalftoneAxis);
            return WaitToComplete(fullscreen);
        }

        static private IEnumerator HandleScenePreReady(Scene scene, StringHash32 tag, MainSceneTransitionParameters transitionArgs) {
            yield break;
        }

        static private IEnumerator HandleSceneLoaded(Scene scene, StringHash32 tag, MainSceneTransitionParameters transitionArgs) {
            if (!transitionArgs.IsInitialLoad) {
                Game.Input.ResumeAll();
            }
            
            Find.State(out FullscreenTransitionState fullscreen);
            FullscreenTransitionState.Mode mode = fullscreen.HoldMode;
            if (transitionArgs.TransitionType == "dither") {
                mode = FullscreenTransitionState.Mode.Dither;
            } else if (transitionArgs.TransitionType == "subtract") {
                mode = FullscreenTransitionState.Mode.Subtract;
            } else if (transitionArgs.TransitionType == "fade") {
                mode = FullscreenTransitionState.Mode.Fade;
            } else if (transitionArgs.TransitionType == "halftone") {
                mode = FullscreenTransitionState.Mode.Halftone;
            }

            FadeIn(fullscreen, mode, fullscreen.DefaultTransitionTime, FullscreenTransitionFlags.FlipHalftoneAxis);
            return null;
        }

        static public void FadeOut(FullscreenTransitionState fullscreen, float duration, FullscreenTransitionFlags flags) {
            FadeOut(fullscreen, fullscreen.DefaultMode, duration, flags);
        }

        static public void FadeOut(FullscreenTransitionState fullscreen, FullscreenTransitionState.Mode mode, float duration, FullscreenTransitionFlags flags) {
            if ((flags & FullscreenTransitionFlags.FlipHalftoneAxis) != 0) {
                fullscreen.HalftoneDirection = (FullscreenTransitionState.HalftoneAxis)(1 - (int)fullscreen.HalftoneDirection);
            }

            if (fullscreen.CurrentMode == FullscreenTransitionState.Mode.Fullscreen) {
                return;
            }

            if (mode == FullscreenTransitionState.Mode.Off || mode == FullscreenTransitionState.Mode.Fullscreen || duration <= 0) {
                CutTo(fullscreen);
                return;
            }

            fullscreen.CurrentMode = mode;
            fullscreen.ActiveState = true;
            fullscreen.CurrentTransitionSpeed = 1 / duration;
            fullscreen.Renderer.enabled = true;

            ComputeHalftoneNormals(fullscreen);
        }

        static public void FadeIn(FullscreenTransitionState fullscreen, float duration, FullscreenTransitionFlags flags) {
            if (fullscreen.HoldMode != FullscreenTransitionState.Mode.Off) {
                FadeIn(fullscreen, fullscreen.HoldMode, duration, flags);
            } else {
                FadeIn(fullscreen, fullscreen.DefaultMode, duration, flags);
            }
        }

        static public void FadeIn(FullscreenTransitionState fullscreen, FullscreenTransitionState.Mode mode, float duration, FullscreenTransitionFlags flags) {
            if ((flags & FullscreenTransitionFlags.FlipHalftoneAxis) != 0) {
                fullscreen.HalftoneDirection = (FullscreenTransitionState.HalftoneAxis)(1 - (int)fullscreen.HalftoneDirection);
            }

            if (fullscreen.CurrentMode == FullscreenTransitionState.Mode.Off) {
                return;
            }

            if (mode == FullscreenTransitionState.Mode.Off || duration <= 0) {
                Clear(fullscreen);
                return;
            }

            fullscreen.CurrentMode = mode;
            fullscreen.ActiveState = false;
            fullscreen.CurrentTransitionSpeed = 1 / duration;

            ComputeHalftoneNormals(fullscreen);
        }

        static private void ComputeHalftoneNormals(FullscreenTransitionState fullscreen) {
            if (fullscreen.CurrentMode != FullscreenTransitionState.Mode.Halftone) {
                return;
            }

            float tilt = RNG.Instance.NextFloat(fullscreen.MaxHalftoneTilt * 0.5f, fullscreen.MaxHalftoneTilt) * RNG.Instance.SignNonZero();
            if (fullscreen.HalftoneDirection == FullscreenTransitionState.HalftoneAxis.Horizontal) {
                fullscreen.HalftoneNormal = new Vector2(1, tilt).normalized;
            } else {
                fullscreen.HalftoneNormal = new Vector2(tilt, 1).normalized;
            }
        }

        static public bool IsTransitionDone(FullscreenTransitionState fullscreen) {
            return fullscreen.CurrentMode == (fullscreen.ActiveState ? FullscreenTransitionState.Mode.Fullscreen : FullscreenTransitionState.Mode.Off);
        }

        static public bool IsTransitionFullyFadedOut(FullscreenTransitionState fullscreen) {
            return fullscreen.CurrentMode == FullscreenTransitionState.Mode.Fullscreen;
        }

        static public IEnumerator WaitToComplete(FullscreenTransitionState fullscreen) {
            while(!IsTransitionDone(fullscreen)) {
                yield return null;
            }
        }

        static public void Clear(FullscreenTransitionState fullscreen) {
            fullscreen.CurrentTransitionFactor = 0;
            fullscreen.CurrentMode = fullscreen.HoldMode = FullscreenTransitionState.Mode.Off;
            fullscreen.Renderer.enabled = false;
            fullscreen.ActiveState = false;
        }

        static public void CutTo(FullscreenTransitionState fullscreen) {
            fullscreen.CurrentTransitionFactor = 1;
            fullscreen.CurrentMode = fullscreen.HoldMode = FullscreenTransitionState.Mode.Fullscreen;

            fullscreen.Renderer.sharedMaterial = fullscreen.ColorTransitionMaterial;
            
            fullscreen.Renderer.enabled = true;
            fullscreen.ActiveState = false;
        }

        static public void CutTo(FullscreenTransitionState fullscreen, Color color) {
            fullscreen.CurrentTransitionFactor = 1;
            fullscreen.CurrentMode = fullscreen.HoldMode = FullscreenTransitionState.Mode.Fullscreen;

            fullscreen.CurrentColor = color;
            fullscreen.Renderer.sharedMaterial = fullscreen.ColorTransitionMaterial;

            fullscreen.Renderer.enabled = true;
            fullscreen.ActiveState = true;
        }

        static public void SetColor(FullscreenTransitionState fullscreen, Color color) {
            fullscreen.CurrentColor = color;
        }

        static public void ResetColor(FullscreenTransitionState fullscreen) {
            fullscreen.CurrentColor = fullscreen.DefaultColor;
        }
    }

    [Flags]
    public enum FullscreenTransitionFlags {
        FlipHalftoneAxis = 0x01
    }
}
