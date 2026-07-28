using System;
using System.Collections.Generic;
using UnityEngine;

namespace Splice.Data
{
    [Serializable]
    public sealed class WorldMapNodeDefinition
    {
        public string nodeId;
        public string displayName;
        public WorldNodeKind kind;
        public Vector2 mapPosition;
        public string destinationScene;
        public string contentId;
        [Min(0)] public int requiredPlayerLevel;
        public List<string> prerequisiteNodeIds = new();
    }

    [CreateAssetMenu(menuName = "Splice/Maps/World Map Definition", fileName = "WorldMapDefinition")]
    public sealed class WorldMapDefinitionSO : MapDefinitionSO
    {
        [SerializeField] private List<WorldMapNodeDefinition> nodes = new();
        public IReadOnlyList<WorldMapNodeDefinition> Nodes => nodes;
    }
}
