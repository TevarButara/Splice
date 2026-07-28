using UnityEngine;

namespace Veridian.RockGenLite.Runtime
{
    /// <summary>
    /// Records the deterministic recipe used to build an exported cluster.
    /// The generated children remain normal editable GameObjects.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class RockClusterGroup : MonoBehaviour
    {
        [SerializeField] private int seed;
        [SerializeField] private int rockCount;
        [SerializeField] private RockClusterShape shape;

        public int Seed => seed;
        public int RockCount => rockCount;
        public RockClusterShape Shape => shape;

        public void Configure(int clusterSeed, int count, RockClusterShape clusterShape)
        {
            seed = clusterSeed;
            rockCount = count;
            shape = clusterShape;
        }
    }
}
