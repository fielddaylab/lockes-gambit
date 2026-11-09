using System.Collections.Generic;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using SpaceFab.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    public sealed class SupplyErrorPanel : SharedStateComponent, IRegistrationCallbacks {
        public CanvasGroup PopupGroup;
        public ShipListRow Ship;
        public GameObject DuplicatePanel, UnidentifiedPanel, NotRequiredPanel, DopantMismatchPanel, MissingSemiconductorPanel, NeedsResearchPanel;
        public GuiButton CloseButton;
        public CanvasInputLayer Input;
        public Canvas Canvas;

        private enum SupplyErrorType : byte {
            None,
            Pending,
            Unidentified, // No properties found
            MissingSemiconductor, // No semiconductor has been collected.
            DopantMismatch,  // Semiconductor collected, but dopant context doesn't match.
            Duplicate,
            NeedsResearch, // Material can fulfill the requirement, but property not confirmed.
            NotRequired
        }

        private static readonly HashSet<StringHash32> s_GatheredScratch = new HashSet<StringHash32>();
        private static readonly HashSet<StringHash32> s_OtherScratch = new HashSet<StringHash32>();
        private static readonly HashSet<StringHash32> s_ReservedScratch = new HashSet<StringHash32>();
        

        void IRegistrationCallbacks.OnRegister() {
            SpacefabGame.Events.Register<SupplyRouteEventArgs>(GameEvents.SupplyRouteDrawingClose, OnRouteEnded);
            CloseButton.OnClick.Register(OnCloseClicked);
            SetVisible(false);
        }

        void IRegistrationCallbacks.OnDeregister() {
            Game.Events.DeregisterAllForContext(this);
            CloseButton.OnClick.Deregister(OnCloseClicked);

            if (Input.TryPopPriority()) {
                PopupUtility.PopState();
            }
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

            ContractRequirementResolver.RequirementsTable table = contractState.CachedRequirements;
            var records = progress.MaterialProperties;
            bool omitInsulator = SupplyRequirementUtility.IsInsulatorOmitted(contractState.ContractDefinition);

            GatherMaterials(s_OtherScratch, routes, evtArgs, false);
            GatherMaterials(s_GatheredScratch, routes, evtArgs, true);

            int count = evtArgs.Stats.MaterialCount;
            uint[] hashes = new uint[count];
            StringHash32[] ids = new StringHash32[count];
            SupplyErrorType[] reasons = new SupplyErrorType[count];

            // Pass 1: Unidentified, Missing semiconductor, Dopant mismatch
            for (int i = 0; i < count; i++) {
                hashes[i] = evtArgs.Stats.MaterialHashes[i];
                ids[i] = new StringHash32(hashes[i]);

                if (!progress.MaterialProperties.TryGetValue(ids[i], out MaterialPropertyRecord record)
                    || MaterialPropertyRecordUtility.IsEmpty(record)) {
                    reasons[i] = SupplyErrorType.Unidentified;
                    continue;
                }

                SupplyRequirementUtility.DopantRole role = SupplyRequirementUtility.ClassifyDopant(
                    table, records, omitInsulator, s_GatheredScratch, ids[i], record);

                switch (role) {
                    case SupplyRequirementUtility.DopantRole.MissingSemiconductor:
                        reasons[i] = SupplyErrorType.MissingSemiconductor;
                        break;
                    case SupplyRequirementUtility.DopantRole.Mismatch:
                        reasons[i] = SupplyErrorType.DopantMismatch;
                        break;
                    case SupplyRequirementUtility.DopantRole.NeedsResearch:
                        reasons[i] = SupplyErrorType.NeedsResearch;
                        break;
                    default:
                        reasons[i] = SupplyErrorType.Pending;
                        break;
                }
            }

            // Pass 2: Duplicate, Needs research, Not required
            SupplyRequirementUtility.FindReservedDopants(table, records, s_GatheredScratch, s_ReservedScratch);
            int baseline = SupplyRequirementUtility.CountSatisfied(table, records, omitInsulator, s_GatheredScratch, s_ReservedScratch);
            
            for (int i = count - 1; i >= 0; i--) {
                if (reasons[i] != SupplyErrorType.Pending) {
                    continue;
                }

                StringHash32 id = ids[i];
                bool repeated = s_OtherScratch.Contains(id) || AppearsEarlier(ids, i);
                bool needed = false;

                if (!repeated) {
                    s_GatheredScratch.Remove(id);
                    needed = SupplyRequirementUtility.CountSatisfied(table, records, omitInsulator, s_GatheredScratch, s_ReservedScratch) < baseline;
                    if (needed) {
                        s_GatheredScratch.Add(id);
                    }
                }

                if (needed) {
                    reasons[i] = SupplyErrorType.None;
                } else if (SupplyRequirementUtility.IsCandidate(table, records, omitInsulator, id)) {
                    reasons[i] = SupplyErrorType.Duplicate;
                }
                else if (SupplyRequirementUtility.IsRowFilledByOther(table, records, omitInsulator, s_GatheredScratch, s_ReservedScratch, id)) {
                    reasons[i] = SupplyErrorType.NotRequired;
                } else if (SupplyRequirementUtility.IsInTable(table, omitInsulator, id)) {
                    reasons[i] = SupplyErrorType.NeedsResearch;
                } else {
                    reasons[i] = SupplyErrorType.NotRequired;
                }
            }

            uint[] excessMaterials = new uint[count];
            int excessCount = 0;
            bool hasDuplicate = false;
            bool hasUnidentified = false;
            bool hasNotRequired = false;
            bool hasDopantMismatch = false;
            bool hasNoContextDopant = false;
            bool hasNeedsResearch = false;

            for (int i = 0; i < count; i++) {
                switch (reasons[i]) {
                    case SupplyErrorType.Unidentified: hasUnidentified = true; break;
                    case SupplyErrorType.MissingSemiconductor: hasNoContextDopant = true; break;
                    case SupplyErrorType.DopantMismatch: hasDopantMismatch = true; break;
                    case SupplyErrorType.Duplicate: hasDuplicate = true; break;
                    case SupplyErrorType.NeedsResearch: hasNeedsResearch = true; break;
                    case SupplyErrorType.NotRequired: hasNotRequired = true; break;
                    default: continue; // None
                }
                excessMaterials[excessCount++] = hashes[i];
            }

            if (excessCount == 0) {
                return;
            }

            DuplicatePanel.SetActive(hasDuplicate);
            UnidentifiedPanel.SetActive(hasUnidentified);
            NotRequiredPanel.SetActive(hasNotRequired);
            DopantMismatchPanel.SetActive(hasDopantMismatch);
            MissingSemiconductorPanel.SetActive(hasNoContextDopant);
            NeedsResearchPanel.SetActive(hasNeedsResearch);

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

        private static bool AppearsEarlier(StringHash32[] ids, int index) {
            for (int j = 0; j < index; j++) {
                if (ids[j] == ids[index]) {
                    return true;
                }
            }
            return false;
        }

        private static unsafe void GatherMaterials(HashSet<StringHash32> output, SupplyRouteCollection routes, SupplyRouteEventArgs route, bool includeCurrent) {
            output.Clear();
            for (int shipIndex = 0; shipIndex < routes.RouteStats.Length; shipIndex++) {
                bool isCurrent = shipIndex == route.RouteIndex;
                if (isCurrent && !includeCurrent) {
                    continue;
                }

                SupplyRouteStats stats = isCurrent ? route.Stats : routes.RouteStats[shipIndex];
                for (int i = 0; i < stats.MaterialCount; i++) {
                    output.Add(new StringHash32(stats.MaterialHashes[i]));
                }
            }
        }

        private void SetVisible(bool visible) {
            if (visible) {
                if (Input.TryPushPriority()) {
                    PopupUtility.PushState();
                }
            } else {
                if (Input.TryPopPriority()) {
                    PopupUtility.PopState();
                }
            }

            PopupGroup.alpha = visible ? 1 : 0;
            PopupGroup.interactable = visible;
            PopupGroup.blocksRaycasts = visible;
            Canvas.enabled = visible;
            Input.SetInputOverride(visible ? null : false);
        }
    }
}