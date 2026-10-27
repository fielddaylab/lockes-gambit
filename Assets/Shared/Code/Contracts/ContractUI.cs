using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpaceFab.Research;
using FieldDay;
using SpaceFab.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using System;

namespace SpaceFab {
    public class ContractUI : MonoBehaviour
    {
        public TMP_Text Title;
        public TMP_Text Description;

        [Header("Type")]
        public Graphic[] TypeBackground;
        public Graphic[] TypeOutline;
        public TMP_Text TypeLabel;
        public Image TypeSprite;
        public Image TypeSpriteSparkles;

        [Header("Difficulty")]
        public Graphic DifficultyBackground;
        public TMP_Text DifficultyLabel;

        [Header("Chip Diagram")]
        public Graphic ChipDiagramEmpty;
        public Graphic ChipDiagramFilled;

        [Header("Client")]
        public TMP_Text ClientText;
        public Image ClientIcon;

        [Header("Requirements")]
        public ContractRequirementTable Requirements;

        [Header("Stats")]
        public TMP_Text TimeLabel;
        public GuiCounter TimeIndicators;
        public TMP_Text RevenueLabel;
        public GuiCounter RevenueIndicators;

        [Header("Buttons")]
        public GuiButton SelectContractButton;
        public GuiButton ContextBackButton;
        public GuiButton ContextContinueButton;
        public GuiButton CloseButton;
        
        [Header("Animated")]
        public GameObject ApprovedStamp;
        public Image SignatureImage;
        public DitherFadeGroup ContentsMask;
        public DitherFadeGroup ApprovedStampMask;
        public DitherFadeGroup ChipDiagramMask;
        public DitherFadeGroup ContextContentsMask;

        [Header("Context Panel")]
        public Canvas ContextSubCanvas;
        public RectTransform ContextPanel;
        public CanvasGroup ContextFader;
        public TMP_Text ContextHeader;
        public TMP_Text ContextDescription;

        public void ClearElements()
        {
            TimeIndicators.ResetValue();
            RevenueIndicators.ResetValue();
        }

        public void ShowDuration(int duration)
        {
            ((TimeMeterStyle) TimeIndicators.Styler).SetExpectedSlots(0);
            TimeIndicators.SetValue(duration);
        }

        public void ShowProfit(int profit)
        {
            ((MoneyMeterStyle) RevenueIndicators.Styler).SetExpectedSlots(0);
            RevenueIndicators.SetValue(profit);
        }

        public void ShowRequirement(int chapterIndex, ContractDef contract)
        {
            ContractUIUtility.BuildRequirementData(Requirements, chapterIndex, contract);
            ContractUIUtility.InitializeVisuals(Requirements);
            ContractUIUtility.UpdateVisualsWithCompletion(Requirements, Find.State<PlayerProgressState>().MaterialProperties);
        }

        public void ShowDesignCompleted(bool completed) {
            ChipDiagramEmpty.enabled = !completed;
            ChipDiagramFilled.enabled = completed;
            ChipDiagramMask.SetAlpha(1);
        }
    }

    public static partial class ContractUtility
    {
        public static void LoadContractUI(ContractUI ui, int chapterIndex, ContractDef def)
        {
            Find.GlobalAsset(out ContractUIAssetSet assets);

            if (def == null)
            {
                ui.Title.SetText(string.Empty);
                ui.Description.SetText(string.Empty);
            }
            else
            {
                ui.Title.SetText(def.Title());
                ui.Description.SetText(def.Description());
                ui.ClearElements();
                ui.ShowDuration(def.ExpectedDuration());
                ui.ShowProfit(def.Payout());
                ui.ShowRequirement(chapterIndex, def);

                var clientConfig = ContractUIUtility.GetClientInfo(assets, def.ClientId());
                var difficultyConfig = assets.Difficulties[(int)def.Difficulty()];
                var typeConfig = assets.Types[(int)def.ContractClass()];

                ui.ClientText.SetText(clientConfig.Text);
                ui.ClientIcon.sprite = clientConfig.Icon;

                ui.DifficultyBackground.color = difficultyConfig.Background;
                ui.DifficultyLabel.SetText(difficultyConfig.Label);

                ui.TypeSpriteSparkles.enabled = typeConfig.ShowSparkles;
                ui.TypeSprite.sprite = typeConfig.Icon;
                foreach (var bg in ui.TypeBackground) {
                    bg.color = typeConfig.Background;
                }
                foreach (var outline in ui.TypeOutline) {
                    outline.color = typeConfig.Outline;
                }
                ui.TypeLabel.SetText(typeConfig.Label);
            }
        }

        static public void ApplyContractProgressToUI(ContractUI ui, ContractDef def) {
            Find.State(out MinigameSaveStates minigameStates);

            ui.ShowDesignCompleted(minigameStates.Design.FoundValidSolution);

            ui.SignatureImage.enabled = true;
            ui.SignatureImage.fillAmount = 1;

            ui.TimeLabel.SetText("Time");

            TimeMeterStyle timeStyle = (TimeMeterStyle) ui.TimeIndicators.Styler;
            MoneyMeterStyle moneyStyle = (MoneyMeterStyle) ui.RevenueIndicators.Styler;

            timeStyle.SetExpectedSlots(def.ExpectedDuration());
            moneyStyle.SetExpectedSlots(def.Payout());

            int currentDuration = MinigameSaveUtility.CurrentTime(minigameStates);
            int currentCost = MinigameSaveUtility.CurrentCosts(minigameStates);

            ui.RevenueIndicators.SetValue(def.Payout() - currentCost, GuiWidgetUpdateFlags.Initialization);
            ui.TimeIndicators.SetValue(currentDuration, GuiWidgetUpdateFlags.Initialization);
        }

        static public void ApplyNoProgressToUI(ContractUI ui, ContractDef def) {
            ui.SignatureImage.enabled = false;
            ui.ShowDesignCompleted(false);

            ui.TimeLabel.SetText("Expected Time");
        }
    }
}
