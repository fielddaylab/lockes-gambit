using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    public sealed class SupplyErrorPanel : SharedStateComponent, IRegistrationCallbacks {
        public CanvasGroup PopupGroup;
        public ShipListRow Ship;
        public GameObject DuplicatePanel, UnidentifiedPanel, NotRequiredPanel, DopantPanel;
        public GuiButton CloseButton;

        void IRegistrationCallbacks.OnRegister() {
            SpacefabGame.Events.Register<SupplyRouteEventArgs>(GameEvents.SupplyRouteDrawingClose, OnRouteEnded);
            CloseButton.OnClick.Register(OnCloseClicked);
            SetVisible(false);
        }

        void IRegistrationCallbacks.OnDeregister() {
            Game.Events.DeregisterAllForContext(this);
            CloseButton.OnClick.Deregister(OnCloseClicked);
        }

        private void OnCloseClicked(PointerListener.EventData evt) {
            SetVisible(false);
        }

        private unsafe void OnRouteEnded(SupplyRouteEventArgs evtArgs) {
            if (evtArgs.Stats.MaterialCount == 0) {
                return;
            }

            Find.State(out SupplyRouteCollection routes, out SupplyShipIndex ships, out PlayerProgressState progress);
            Find.State(out ContractState contractState);

            MaterialPropertyCheck[] required = contractState.ContractDefinition.RequiredMaterialProperties();
            MaterialPropertyCheck[] omitted = contractState.ContractDefinition.OmitFromSupplyRequirements();
            bool[] fulfilled = new bool[required != null ? required.Length : 0];

            MarkPreviouslyFulfilledRequirements(fulfilled, required, omitted, routes, evtArgs.RouteIndex, progress);

            uint[] excessMaterials = new uint[evtArgs.Stats.MaterialCount];
            int excessCount = 0;
            bool hasDuplicate = false;
            bool hasUnidentified = false;
            bool hasNotRequired = false;
            bool hasDopantMismatch = false;

            for (int i = 0; i < evtArgs.Stats.MaterialCount; i++) {
                uint materialHash = evtArgs.Stats.MaterialHashes[i];
                StringHash32 materialId = new StringHash32(materialHash);
                if (!progress.MaterialProperties.TryGetValue(materialId, out MaterialPropertyRecord record)
                    || MaterialPropertyRecordUtility.IsEmpty(record)) {
                    excessMaterials[excessCount++] = materialHash;
                    hasUnidentified = true;
                    continue;
                }

                bool matchesRequirement = false;
                bool fillsRequirement = false;
                if (required != null) {
                    for (int reqIndex = 0; reqIndex < required.Length; reqIndex++) {
                        if (IsOmitted(required[reqIndex], omitted)
                            || !ContractProgressUtility.SatisfiesCheck(record, required[reqIndex])) {
                            continue;
                        }

                        matchesRequirement = true;
                        fillsRequirement |= !fulfilled[reqIndex];
                    }
                }

                if (fillsRequirement) {
                    for (int reqIndex = 0; reqIndex < required.Length; reqIndex++) {
                        if (!IsOmitted(required[reqIndex], omitted)
                            && ContractProgressUtility.SatisfiesCheck(record, required[reqIndex])) {
                            fulfilled[reqIndex] = true;
                        }
                    }
                    continue;
                }

                excessMaterials[excessCount++] = materialHash;
                if (matchesRequirement) {
                    hasDuplicate = true;
                } else if (HasDopantMismatch(materialId, i, record, routes, evtArgs, progress)) {
                    hasDopantMismatch = true;
                } else {
                    hasNotRequired = true;
                }
            }

            if (excessCount == 0) {
                return;
            }

            DuplicatePanel.SetActive(hasDuplicate);
            UnidentifiedPanel.SetActive(hasUnidentified);
            NotRequiredPanel.SetActive(hasNotRequired);
            DopantPanel.SetActive(hasDopantMismatch);

            ShipListPanel shipList = Find.Panel<ShipListPanel>();
            SupplyChainUtility.PopulateShipInformation(Ship, ships.ShipAssets[evtArgs.RouteIndex], shipList);
            Ship.ShipIndex = evtArgs.RouteIndex;
            Assert.True(excessCount <= Ship.SlotMaterials.Length, "Ship popup has fewer material slots than its route capacity");

            for (int i = 0; i < Ship.SlotMaterials.Length; i++) {
                Image slot = Ship.SlotMaterials[i];
                if (i >= excessCount) {
                    slot.enabled = false;
                    continue;
                }

                MaterialAsset material = Find.NamedAsset<MaterialAsset>(new StringHash32(excessMaterials[i]));
                Assert.True(material != null, "Supply route references a missing material asset");
                slot.sprite = material.GemSprite;
                slot.color = Color.white;
                slot.enabled = true;
            }

            SetVisible(true);
        }

        private static unsafe void MarkPreviouslyFulfilledRequirements(bool[] fulfilled, MaterialPropertyCheck[] required,
            MaterialPropertyCheck[] omitted, SupplyRouteCollection routes, int routeIndex, PlayerProgressState progress) {
            if (required == null) {
                return;
            }

            for (int shipIndex = 0; shipIndex < routes.RouteStats.Length; shipIndex++) {
                if (shipIndex == routeIndex) {
                    continue;
                }

                SupplyRouteStats stats = routes.RouteStats[shipIndex];
                for (int materialIndex = 0; materialIndex < stats.MaterialCount; materialIndex++) {
                    StringHash32 materialId = new StringHash32(stats.MaterialHashes[materialIndex]);
                    if (!progress.MaterialProperties.TryGetValue(materialId, out MaterialPropertyRecord record)) {
                        continue;
                    }

                    for (int reqIndex = 0; reqIndex < required.Length; reqIndex++) {
                        if (!IsOmitted(required[reqIndex], omitted)
                            && ContractProgressUtility.SatisfiesCheck(record, required[reqIndex])) {
                            fulfilled[reqIndex] = true;
                        }
                    }
                }
            }
        }

        private static bool IsOmitted(MaterialPropertyCheck check, MaterialPropertyCheck[] omitted) {
            if (omitted == null) {
                return false;
            }

            for (int i = 0; i < omitted.Length; i++) {
                if (ReferenceEquals(check, omitted[i])) {
                    return true;
                }
            }
            return false;
        }

        private static unsafe bool HasDopantMismatch(StringHash32 materialId, int candidateMaterialIndex, in MaterialPropertyRecord record,
            SupplyRouteCollection routes, SupplyRouteEventArgs route, PlayerProgressState progress) {
            bool isSemiconductor = IsSemiconductor(record);
            bool isDopant = MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.PDopantFor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.NDopantFor);

            if (!isSemiconductor && !isDopant) {
                return false;
            }

            bool hasMatchingSemiconductor = false;
            bool hasMatchingDopant = false;

            for (int shipIndex = 0; shipIndex < routes.RouteStats.Length; shipIndex++) {
                SupplyRouteStats stats = shipIndex == route.RouteIndex ? route.Stats : routes.RouteStats[shipIndex];
                for (int materialIndex = 0; materialIndex < stats.MaterialCount; materialIndex++) {
                    if (shipIndex == route.RouteIndex && materialIndex == candidateMaterialIndex) {
                        continue;
                    }

                    StringHash32 otherId = new StringHash32(stats.MaterialHashes[materialIndex]);
                    if (!progress.MaterialProperties.TryGetValue(otherId, out MaterialPropertyRecord otherRecord)) {
                        continue;
                    }

                    if (isDopant && IsSemiconductor(otherRecord)
                        && IsConfirmedDopantFor(record, otherId)) {
                        hasMatchingSemiconductor = true;
                    }
                    if (isSemiconductor && IsDopantRecord(otherRecord)
                        && IsConfirmedDopantFor(otherRecord, materialId)) {
                        hasMatchingDopant = true;
                    }
                }
            }

            return (isDopant && !hasMatchingSemiconductor) || (isSemiconductor && !hasMatchingDopant);
        }

        private static bool IsSemiconductor(in MaterialPropertyRecord record) {
            return MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.Semiconductor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.HiTempSemiConductor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.LightEmittingSemiconductor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.HighVoltageSemiconductor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.HighMobilitySemiconductor);
        }

        private static bool IsDopantRecord(in MaterialPropertyRecord record) {
            return MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.PDopantFor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.NDopantFor);
        }

        private static bool IsConfirmedDopantFor(in MaterialPropertyRecord record, StringHash32 semiconductorId) {
            return MaterialPropertyRecordUtility.Has(record, MaterialPropertyLabel.PDopantFor, semiconductorId)
                || MaterialPropertyRecordUtility.Has(record, MaterialPropertyLabel.NDopantFor, semiconductorId);
        }

        private void SetVisible(bool visible) {
            PopupGroup.alpha = visible ? 1 : 0;
            PopupGroup.interactable = visible;
            PopupGroup.blocksRaycasts = visible;
        }
    }
}
