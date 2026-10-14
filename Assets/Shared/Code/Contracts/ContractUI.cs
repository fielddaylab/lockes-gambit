using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpaceFab.Research;
using FieldDay;
using SpaceFab.UI;
using FieldDay.UI.Widgets;

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
        public Image[] TimeIndicators;
        public Image[] RevenueIndicators;

        [Header("Buttons")]
        public GuiButton SelectContractButton;
        public GuiButton ContextBackButton;
        public GuiButton ContextContinueButton;
        public GuiButton CloseButton;
        
        [Header("Animated")]
        public GameObject ApprovedStamp;
        public Image SignatureImage;
        public DitherFadeGroup ApprovedStampMask;
        public DitherFadeGroup ChipDiagramMask;
        public DitherFadeGroup ControlsMask;
        public DitherFadeGroup ContextContentsMask;

        [Header("Context Panel")]
        public Canvas ContextSubCanvas;
        public RectTransform ContextPanel;
        public TMP_Text ContextHeader;
        public TMP_Text ContextDescription;

        public void ClearElements()
        {
            Find.GlobalAsset<ContractMeterSpriteSet>(out ContractMeterSpriteSet spriteSet);

            for (int i = 0; i < TimeIndicators.Length; i++) {
                TimeIndicators[i].sprite = spriteSet.TimeEmpty;
            }
            for (int i = 0; i < RevenueIndicators.Length; i++) {
                RevenueIndicators[i].sprite = spriteSet.RevenueEmpty;
            }
        }

        public void ShowDuration(int duration)
        {
            Find.GlobalAsset<ContractMeterSpriteSet>(out ContractMeterSpriteSet spriteSet);

            for (int i = 0; i < duration; i++) {
                TimeIndicators[i].sprite = spriteSet.TimeFilled;
            }
        }

        public void ShowProfit(int profit)
        {
            Find.GlobalAsset<ContractMeterSpriteSet>(out ContractMeterSpriteSet spriteSet);
            
            for (int i = 0; i < profit; i++) {
                RevenueIndicators[i].sprite = spriteSet.RevenueFilled;
            }
        }

        public void ShowRequirement(int chapterIndex, ContractDef contract)
        {
            ContractUIUtility.BuildRequirementData(Requirements, chapterIndex, contract);
            ContractUIUtility.InitializeVisuals(Requirements);
            ContractUIUtility.UpdateVisualsWithCompletion(Requirements, Find.State<PlayerProgressState>().MaterialProperties);
        }
    }

    public static partial class ContractUtility
    {
        public static void LoadContractData(ContractUI ui, int chapterIndex, ContractDef def)
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
    }
}
