#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Veridian.RockGenLite.Runtime;
using System.Collections.Generic;
using System;

namespace Veridian.RockGenLite.Editor
{
    [InitializeOnLoad]
    internal static class EditorGenerationDriver
    {
        private static readonly HashSet<RuntimeRockGenerator> _activeGenerators = new HashSet<RuntimeRockGenerator>();

        // FIX: Use double for timeSinceStartup instead of DateTime to prevent allocations and clock syncing issues
        private static double _lastUpdateTime;
        private const double EDITOR_UPDATE_RATE = 1.0 / 60.0;

        static EditorGenerationDriver()
        {
            // Subscribe to domain reload events
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }
        private static void OnBeforeAssemblyReload()
        {
            // Destroy all hidden processing generators before the domain drops
            // preventing permanent phantom objects in the hierarchy
            foreach (var generator in _activeGenerators)
            {
                if (generator != null && generator.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(generator.gameObject);
                }
            }

            _activeGenerators.Clear();
            EditorApplication.update -= DriveUpdate;
        }
        public static void Register(RuntimeRockGenerator generator)
        {
            if (generator == null || Application.isPlaying) return;
            if (_activeGenerators.Add(generator))
            {
                if (_activeGenerators.Count == 1)
                {
                    EditorApplication.update += DriveUpdate;
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                }
            }
        }

        public static void Unregister(RuntimeRockGenerator generator)
        {
            if (generator == null || Application.isPlaying) return;
            if (_activeGenerators.Remove(generator))
            {
                if (_activeGenerators.Count == 0)
                {
                    EditorApplication.update -= DriveUpdate;
                }
            }
        }

        private static void DriveUpdate()
        {
            if (Application.isPlaying)
            {
                // FIX: Simply return without unsubscribing or clearing the active generators.
                // This allows the preview to safely pause and resume seamlessly in Fast Play Mode.
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastUpdateTime < EDITOR_UPDATE_RATE)
            {
                return;
            }
            _lastUpdateTime = currentTime;

            _activeGenerators.RemoveWhere(g => g == null);

            if (_activeGenerators.Count == 0)
            {
                EditorApplication.update -= DriveUpdate;
                return;
            }

            List<RuntimeRockGenerator> generatorsToUpdate = new List<RuntimeRockGenerator>(_activeGenerators);

            foreach (var generator in generatorsToUpdate)
            {
                if (generator != null)
                {
                    try
                    {
                        generator.EditorUpdate();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error driving RuntimeRockGenerator update in editor: {e.InnerException?.Message ?? e.Message}");
                        _activeGenerators.Remove(generator);
                    }
                }
            }
        }
    }
}
#endif