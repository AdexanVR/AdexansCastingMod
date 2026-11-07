using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

// The auto-updater compares this with the GitHub release tag and the downloaded DLL's own version
[assembly: AssemblyVersion(AdexansCastingMod.PluginInfo.Version)]
[assembly: AssemblyFileVersion(AdexansCastingMod.PluginInfo.Version)]

namespace AdexansCastingMod
{
    // Spectator/casting overlay for competitive Gorilla Tag. It only reads room state that every
    // client already has and renders to the desktop window; it never changes gameplay.
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        // Full path of this DLL as BepInEx loaded it (used by the updater)
        internal static string PluginPath;
        static GameObject runtimeObject;

        void Awake()
        {
            Log = Logger;
            PluginPath = Info != null ? Info.Location : null;
            if (runtimeObject != null) return;

            runtimeObject = new GameObject("AdexansCastingMod");
            DontDestroyOnLoad(runtimeObject);
            runtimeObject.AddComponent<CastingRuntime>();
        }
    }

    internal static class PluginInfo
    {
        public const string GUID = "com.adexan.castingmod";
        public const string Name = "Adexan's Casting Mod";
        // Bump this for every release; release.ps1 tags the GitHub release with it
        public const string Version = "1.1.4";
    }
}
