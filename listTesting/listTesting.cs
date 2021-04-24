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
using UnityEngine.UI;

namespace listTesting
{
    [BepInPlugin("Lookenpeepers-listTesting", "List Testing", "1.0.0")]
    [HarmonyPatch]
    public class listTesting : BaseUnityPlugin
    {
        static List<Container> containerList = new List<Container>();
        static Player _player;
        private static string _output;
        static bool _keyDown;
        public static ConfigEntry<string> keyPullString;
        public static KeyCode configPullKey;
        private static int invSlotCount;
        private static int invWidth;
        private static int invHeight;

        void Awake()
        {
            keyPullString = Config.Bind("1 - List Boxes", "Pull Key", "L", "The key to List boxes. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            configPullKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyPullString.Value);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        private static void PlayerAwake_Patch(Player __instance)
        {
            _player = __instance;
            int x = _player.GetInventory().GetWidth();
            int y = _player.GetInventory().GetHeight();
            invSlotCount = x * y;
            invWidth = x;
            invHeight = y;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        private static void PlayerUpdate_Patch(Player __instance)
        {
            _keyDown = Input.GetKeyDown(configPullKey);
            if (_keyDown)
            {
                _keyDown = false;
                _output = "\n";
                GetHoverItem();
                //CleanChestList();
                //check hover item

            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "OnDestroy")]
        private static void PlayerDestroy_Patch(Player __instance)
        {
            Debug.Log("\n\nPlayer Destroyed\n\n");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Container), "Awake")]
        private static void ContainerAwake_Patch(Container __instance)
        {
            //Debug.Log("Name : " + __instance.name);
            if (CheckValidity(__instance))
            {
                containerList.Add(__instance);
            }
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(Container), "OnDestroyed")]
        //private static void ContainerDestroyed_Patch(Container __instance)
        //{
        //    containerList.Remove(__instance);
        //}
        private static void GetHoverItem()
        {
            ItemDrop.ItemData item = _player.GetInventory().GetItemAt(1, 1);
            string name = item.m_shared.m_name;
            string sname = item.m_dropPrefab.name;
            string hoveritem = _player.GetHoverName();
            _output += name + " : " + sname + "\n" + hoveritem;
            Debug.Log(_output);
        }
        private static void CleanChestList()
        {
            
            containerList = containerList.Where(box => box != null).ToList();
            _output += containerList.Count + "\n";
            Debug.Log(_output);
        }

        private static bool CheckValidity(Container c)
        {
            if (c.GetInventory() != null)
            {
                if ((c.name.Contains("chest") || c.name.Contains("Container")))
                {
                    long ID = _player.GetPlayerID();
                    if (Traverse.Create(c).Method("CheckAccess", new object[] { ID }).GetValue<bool>())
                    {
                        Traverse.Create(c).Method("Load").GetValue();
                        return true;
                    }
                }
            }                      
            return false;
        }
    }
}
