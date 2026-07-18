using System.Collections.Generic;
using Splice.Data;
using UnityEngine;

namespace Splice.Base
{
    // สร้างรายชื่อเป้าหมาย raid (roadmap 5.4). greybox: **ฐาน bot generate** จาก registry → ไม่มี cold start
    // (มีเป้าให้ตีตลอดแม้ยังไม่มีผู้เล่นจริง). ต่อไปแทนด้วย snapshot ผู้เล่นจริงจาก server.
    // owner = "bot_x" (≠ PlayerProfile.AccountId) → กติกากัน self-farming (attacker≠defender) ผ่านเอง.
    public class RaidTargetProvider : MonoBehaviour
    {
        [SerializeField] private FactionRegistrySO registry;
        [SerializeField] private int targetCount = 5;
        [SerializeField] private int towersPerBase = 3;
        [SerializeField] private int garrisonPerBase = 2;
        [SerializeField] private int minGold = 100;
        [SerializeField] private int maxGold = 500;
        [Tooltip("จุดเริ่มวางฐาน bot (world) — ให้ตรงกับโซนฝ่ายตั้งรับในซีน raid")]
        [SerializeField] private Vector3 baseOrigin = Vector3.zero;
        [SerializeField] private float spacing = 3f;
        [SerializeField] private int perRow = 4;

        public List<RaidTarget> GenerateTargets()
        {
            var list = new List<RaidTarget>();
            if (registry == null || registry.Factions.Count == 0) return list;

            for (var i = 0; i < targetCount; i++)
            {
                var faction = registry.Factions[Random.Range(0, registry.Factions.Count)];
                if (faction == null) continue;

                var layout = new BaseLayout
                {
                    ownerAccountId = "bot_" + i,
                    factionId = faction.factionId,
                    storedGold = Random.Range(minGold, maxGold + 1),
                };

                var slot = 0;
                for (var t = 0; t < towersPerBase && faction.towers.Count > 0; t++)
                {
                    var tower = faction.towers[Random.Range(0, faction.towers.Count)];
                    var id = registry.IdOf(tower);
                    if (tower == null || string.IsNullOrEmpty(id)) continue;
                    layout.towers.Add(new PlacedTowerData { towerId = id, position = SlotPos(slot++) });
                }
                for (var g = 0; g < garrisonPerBase && faction.cards.Count > 0; g++)
                {
                    var card = faction.cards[Random.Range(0, faction.cards.Count)];
                    var id = registry.IdOf(card);
                    if (card == null || card.linkedMonster == null || string.IsNullOrEmpty(id)) continue;
                    layout.garrison.Add(new GarrisonMonsterData { cardId = id, position = SlotPos(slot++) });
                }

                list.Add(new RaidTarget
                {
                    displayName = $"Bot {faction.displayName} #{i + 1}",
                    baseLevel = 1,
                    layout = layout,
                });
            }
            return list;
        }

        private Vector3 SlotPos(int slot)
        {
            var col = slot % Mathf.Max(1, perRow);
            var row = slot / Mathf.Max(1, perRow);
            return baseOrigin + new Vector3(col * spacing, 0f, row * spacing);
        }
    }
}
