using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace GetTotalResources
{
    [BepInPlugin("Lookenpeepers-GetTotalResources", "Get Total Resources", "1.0.0")]
    [HarmonyPatch]
    //Extend base unity plugin
    public class GetTotalResources : BaseUnityPlugin
    {
        static Player player;

        private static ConfigEntry<bool> enableMod;
        public static KeyCode configCountKey;
        public static ConfigEntry<string> keyCountString;
        private static List<Container> containers = new List<Container>();
        private static List<Piece> pieces = new List<Piece>();

        private struct StrInt
        {
            public string name;
            public int amount;
        }
        void Awake()
        {
            enableMod = Config.Bind("1 - Get Total Resources", "Enable Mod", true, "Enable or disable this mod");
            keyCountString = Config.Bind("1 - Scan for items", "Scan key", "H", "The key to use to scan for items. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            configCountKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyCountString.Value);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private static void GetResources()
        {
            List<StrInt> _totals = new List<StrInt>();
            List<ItemDrop.ItemData> Items = new List<ItemDrop.ItemData>();
            List<string> Names = new List<string>();
            foreach (Container c in containers)
            {
                foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems())
                {
                    Items.Add(item);
                    string cleanName = item.m_shared.m_name.Replace("$item_", "");
                    if (!Names.Contains(cleanName))
                    {
                        Names.Add(cleanName);
                    }
                }
            }
            foreach (Piece p in pieces)
            {
                ItemDrop.ItemData item = p.m_resources[0].m_resItem.m_itemData;
                string cleanName = item.m_shared.m_name.Replace("$item_", "");
                if (!Names.Contains(cleanName))
                {
                    Names.Add(cleanName);
                }
            }
            Names.Sort();
            foreach (string name in Names)
            {
                StrInt total = new StrInt();
                total.name = name;
                foreach(Piece p in pieces)
                {
                    if (p.name.Split('_')[0] == name)
                    {
                        total.amount += 50;
                    }
                }
                foreach (ItemDrop.ItemData item in Items)
                {
                    string cleanName = item.m_shared.m_name.Replace("$item_", "");
                    if (cleanName == name)
                    {
                        total.amount += item.m_stack;
                    }
                }
                _totals.Add(total);
            }
            foreach (StrInt total in _totals)
            {
                string _output = total.name + " : " + total.amount;
                Debug.Log(_output);
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        public static void PlayerUpdate_Patch(Player __instance)
        {
            bool keyDown = Input.GetKeyDown(configCountKey);

            if (keyDown)
            {
                keyDown = false;
                //perform inventory check on keypress
                RemoveInvalidContainers();
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        private static void PlayerAwake_Patch(Player __instance)
        {
            player = __instance;
        }
        //Add Valid Containers
        [HarmonyPatch(typeof(Container), "Awake")]
        static class Container_Awake_Patch
        {
            static void Postfix(Container __instance)
            {
                if ((__instance.name.Contains("chest") || __instance.name.Contains("Container")) && __instance.GetInventory() != null)
                {
                    containers.Add(__instance);
                }
            }
        }
        //Remove destroyed containers from list
        [HarmonyPatch(typeof(Container), "OnDestroyed")]
        static class Container_OnDestroyed_Patch
        {
            static void Prefix(Container __instance)
            {
                containers.Remove(__instance);
            }
        }
        //Add Valid Pieces
        [HarmonyPatch(typeof(Piece), "Awake")]
        static class Piece_Awake_Patch
        {
            static void Postfix(Piece __instance)
            {
                if (__instance.name == "wood_stack(Clone)" || __instance.name == "stone_pile(Clone)")
                {
                    pieces.Add(__instance);
                }
            }
        }
        //Remove destroyed pieces from list
        [HarmonyPatch(typeof(Piece), "OnDestroy")]
        static class Piece_OnDestroyed_Patch
        {
            static void Prefix(Piece __instance)
            {
                pieces.Remove(__instance);
            }
        }
        private static void RemoveInvalidContainers()
        {
            List<int> InvalidContainerIndexes = new List<int>();
            foreach (Container c in containers)
            {
                if (c == null)
                {
                    InvalidContainerIndexes.Add(containers.IndexOf(c));
                }
            }
            for (var i = InvalidContainerIndexes.Count - 1; i > -1; i--)
            {
                containers.RemoveAt(InvalidContainerIndexes[i]);
            }
            RemoveInvalidPieces();
        }
        private static void RemoveInvalidPieces()
        {
            List<int> InvalidPieceIndexes = new List<int>();
            foreach (Piece p in pieces)
            {
                if (p == null)
                {
                    InvalidPieceIndexes.Add(pieces.IndexOf(p));
                }
            }
            for (var i = InvalidPieceIndexes.Count - 1; i > -1; i--)
            {
                pieces.RemoveAt(InvalidPieceIndexes[i]);
            }
            GetResources();
        }
    }
}
