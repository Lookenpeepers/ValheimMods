using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AutoDoors
{
    //Initialize BepInEx
    [BepInPlugin("Lookenpeepers-AutoDoors", "Auto Doors", "1.0.0")]
    //[BepInProcess("valheim.exe")]
    [HarmonyPatch]
    //Extend BaseUnityPlugin
    public class AutoDoors : BaseUnityPlugin
    {
        private static ConfigEntry<bool> enableMod;
        public static ConfigEntry<float> range;
        void Awake()
        {
            enableMod = Config.Bind("1 - General", "Enable Mod", true, "Enable or disable this mod");
            range = Config.Bind<float>("1 - General", "Door Range", 5f, "The maximum range a player should be from a door for it to open");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private static Player _player;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        public static void PlayerAwake_Patch(Player __instance)
        {
            _player = __instance;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        public static void PlayerUpdate_Patch()
        {
            //get doors
        }
    }
}
