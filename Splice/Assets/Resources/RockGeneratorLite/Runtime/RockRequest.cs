using System;
using UnityEngine;

namespace Veridian.RockGenLite.Runtime
{
    public class RockRequest
    {
        public RockSettings Settings;
        public Vector3 Position;
        public Quaternion Rotation;

        public Vector3 Scale;
        public Material SharedMaterial;

        public bool GenerateColliders; // NEW: Flag to control physics generation

        public Action<GameObject> OnComplete;

        public RockRequest(RockSettings settings, Vector3 position, Quaternion rotation, Vector3 scale, Material sharedMaterial, bool generateColliders, Action<GameObject> onComplete)
        {
            Settings = settings;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            SharedMaterial = sharedMaterial;
            GenerateColliders = generateColliders;
            OnComplete = onComplete;
        }
    }
}