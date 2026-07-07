using Splice.Combat;
using Splice.Data;
using Splice.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Splice.UI
{
    // One tower card in the Fort/Defender build bar (mirrors MonsterCardView, but simpler — towers place
    // instantly on the grid, so there's no queue/cooldown/stack). Self-updating (greybox, no events): it
    // greys out when the player can't afford the tower and highlights while this tower is the armed one.
    // Tap -> arms it in TowerPlacementInputController; the player then taps a grid cell to build.
    public class TowerCardView : MonoBehaviour
    {
        [SerializeField] private TowerPlacementInputController placement;
        [SerializeField] private TowerDefinitionSO tower;

        [Header("Widgets")]
        [SerializeField] private Button button;
        [Tooltip("หรี่เป็นสีเทาเมื่อเงินไม่พอ")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("ไฮไลต์ที่โชว์ตอนป้อมนี้กำลังถูกเลือก (ถืออยู่จะวาง) — ปิดไว้ตอนเริ่ม")]
        [SerializeField] private GameObject selectedHighlight;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClickSelect);
        }

        // Also wire this to the Button's OnClick in the Inspector if you prefer not to rely on Awake.
        public void OnClickSelect()
        {
            if (placement == null || tower == null) return;
            var id = placement.IdOf(tower);
            if (!string.IsNullOrEmpty(id)) placement.SelectTower(id);
        }

        private void Update()
        {
            if (tower == null || placement == null) return;

            var bank = GoldController.For(placement.DeployTeam);
            var affordable = bank != null && bank.CurrentGold >= tower.goldCost;

            if (canvasGroup != null) canvasGroup.alpha = affordable ? 1f : 0.4f;
            if (button != null) button.interactable = affordable;
            if (nameLabel != null) nameLabel.text = tower.displayName;
            if (costLabel != null) costLabel.text = tower.goldCost.ToString();
            if (selectedHighlight != null) selectedHighlight.SetActive(placement.SelectedTowerId == placement.IdOf(tower));
        }
    }
}
