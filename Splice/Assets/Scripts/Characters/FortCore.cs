using Splice.Combat;

namespace Splice.Characters
{
    // The single win/lose objective per raid: monsters win by destroying this (architecture 5.1).
    public class FortCore : TowerCharacter
    {
        public static FortCore Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        // The core is the match objective: when it dies the raid ends (RaidManager observes IsDead), but we
        // KEEP the object in the scene so a destroyed/husk animation can play. So — unlike every other
        // character — it does NOT despawn on death.
        protected override void HandleDeath()
        {
            // Intentionally left blank: no despawn. The husk stays; IsDead is already true.
        }

        // Legacy scenes without a BreachRingController remain damageable. Three-ring raids expose the Core
        // only after every authored defense ring has been breached on the server.
        protected override bool CanReceiveDamage(int amount, CharacterBase source)
        {
            var rings = BreachRingController.Instance;
            return base.CanReceiveDamage(amount, source) &&
                   (rings == null || !rings.HasRingObjectives || rings.CoreUnlocked);
        }
    }
}
