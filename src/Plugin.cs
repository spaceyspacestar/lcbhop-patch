using BepInEx;
using BepInEx.Logging;

using GameNetcodeStuff;

using HarmonyLib;

using UnityEngine;

namespace lcbhop {
    [BepInPlugin( MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION )]
    public class Plugin : BaseUnityPlugin {
        public static ManualLogSource logger;
        private readonly Harmony harmony = new Harmony( MyPluginInfo.PLUGIN_GUID );

        public static Config cfg { get; set; }

        public static bool patchMove = true;
        public static bool patchJump = true;

        public static CPMPlayer player;

        void Awake( ) {
            cfg = new Config( Config );
            cfg.Init( );

            logger = Logger;

            harmony.PatchAll( );

            // Plugin startup logic
            Logger.LogInfo( $"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!" );
        }
    }
}
