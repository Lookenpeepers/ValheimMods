using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DepositAnywhere
{
    //Initialize BepInEx
    [BepInPlugin("Lookenpeepers-DepositAnywhere", "Deposit Anywhere", "1.0.5")]
    //[BepInProcess("valheim.exe")]
    [HarmonyPatch]
    //Extend BaseUnityPlugin
    public class DepositAnywhere : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("Lookenpeepers-DepositAnywhere");

        public static List<Container> containerList = new List<Container>();
        private static ConfigEntry<bool> enableMod;
        public static ConfigEntry<float> range;
        public static ConfigEntry<string> keyDepositString;
        public static ConfigEntry<int> excludedSlots;
        public static KeyCode configDepositKey;
        
        void Awake()
        {
            enableMod = Config.Bind("2 - Global", "Enable Mod", true, "Enable or disable this mod");
            range = Config.Bind<float>("3 - General", "ContainerRange", 10f, "The maximum range to send items");
            keyDepositString = Config.Bind("1 - Deposit All Items", "Deposit All Key", "G", "The key to use to deposit items. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            excludedSlots = Config.Bind("1 - Deposit All Items", "Excluded Slots", 0, "Number of Inventory slots to exclude from depositing.");
            configDepositKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyDepositString.Value);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private static ItemDrop.ItemData GetItemAtIndex(Inventory inv ,int index)
        {
            ItemDrop.ItemData item = null;
            int tmpX;
            int tmpY;
            if (index > 7 && index <= 15)
            {
                tmpY = 1;
                tmpX = index - 8;
                return inv.GetItemAt(tmpX, tmpY);
            }
            else if (index > 15 && index <= 23)
            {
                tmpY = 2;
                tmpX = index - 16;
                return inv.GetItemAt(tmpX, tmpY);
            }
            else if (index > 23)
            {
                tmpY = 3;
                tmpX = index - 24;
                return inv.GetItemAt(tmpX, tmpY);
            }
            return item;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        public static void PlayerUpdate_Patch(Player __instance)
        {
            bool keyDown = Input.GetKeyDown(configDepositKey);

            if (keyDown)
            {
                keyDown = false;
                //remove invalid chests
                List<int> deletables = new List<int>();
                foreach (Container c in containerList)
                {
                    if (c == null)
                    {
                        deletables.Add(containerList.IndexOf(c));
                    }
                }
                for (var i = deletables.Count - 1; i > -1; i--)
                {
                    containerList.RemoveAt(deletables[i]);
                }
                List<Container> boxes = GetNearbyContainers(__instance.transform.position);
                Inventory inventory = __instance.GetInventory();
                for (var i = 8 + excludedSlots.Value; i < 32; i++)
                {
                    ItemDrop.ItemData item = GetItemAtIndex(inventory, i);
                    string itemName = item?.m_shared.m_name;
                    //loop through each chest to find a matching item
                    if (item != null && !item.m_equiped)
                    {
                        for (var j = 0; j < boxes.Count; j++)
                        {
                            Inventory boxInventory = boxes[j].GetInventory();
                            List<string> boxItems = new List<string>();
                            foreach (ItemDrop.ItemData boxItem in boxInventory.GetAllItems())
                            {
                                boxItems.Add(boxItem?.m_shared.m_name);
                            }
                            if (boxItems.Contains(itemName))
                            {
                                boxInventory.MoveItemToThis(inventory, item);
                            }
                        }
                    }
                }
            }
        }
        public static List<Container> GetNearbyContainers(Vector3 center, bool ignoreRange = false)
        {
            List<Container> containers = new List<Container>();
            foreach (Container container in containerList)
            {
                if (container != null && container.transform != null && container.GetInventory() != null && (ignoreRange || range.Value <= 0 || Vector3.Distance(center, container.transform.position) < range.Value) && Traverse.Create(container).Method("CheckAccess", new object[] { Player.m_localPlayer.GetPlayerID() }).GetValue<bool>() && !container.IsInUse())
                {
                    Traverse.Create(container).Method("Load").GetValue();
                    containers.Add(container);
                }
            }
            return containers;
        }
        //Add Valid Containers
        [HarmonyPatch(typeof(Container), "Awake")]
        static class Container_Awake_Patch
        {
            static void Postfix(Container __instance, ZNetView ___m_nview)
            {
                if ((__instance.name.Contains("chest") || __instance.name.Contains("Container")) && __instance.GetInventory() != null)
                {
                    containerList.Add(__instance);
                }
            }
        }
        //Remove destroyed containers from list
        [HarmonyPatch(typeof(Container), "OnDestroyed")]
        static class Container_OnDestroyed_Patch
        {
            static void Prefix(Container __instance)
            {
                containerList.Remove(__instance);
            }
        }
        //Update Hover Text on boxes
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        public static string ContainerGetHoverText_Patch(string __result, Container __instance)
        {
            string result = __result;
            result += $"\n[<color=yellow><b>G</b></color>] Deposit All Items";
            return result;
        }
    }
}
