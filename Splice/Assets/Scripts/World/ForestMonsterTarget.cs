using System;
using UnityEngine;

namespace Splice.World
{
    public sealed class ForestMonsterTarget : MonoBehaviour
    {
        [Min(1), SerializeField] private int maxHealth = 100;
        [Min(0), SerializeField] private int fragmentDropMin = 15;
        [Min(0), SerializeField] private int fragmentDropMax = 25;
        [SerializeField] private int deterministicSeed = 1;
        private int health;

        public bool IsAlive => health > 0;
        public int Health => health;
        public event Action<ForestMonsterTarget, int> Defeated;

        private void Awake() => health = Mathf.Max(1, maxHealth);

        public void TakeDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return;
            health = Mathf.Max(0, health - damage);
            if (health > 0) return;
            var random = new System.Random(deterministicSeed);
            var min = Mathf.Max(0, fragmentDropMin);
            var max = Mathf.Max(min, fragmentDropMax);
            var fragments = random.Next(min, max + 1);
            Defeated?.Invoke(this, fragments);
            gameObject.SetActive(false);
        }
    }
}
