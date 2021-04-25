using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DepositAnywhere
{
    //Initialize BepInEx
    [BepInPlugin("Lookenpeepers-DepositAnywhere", "Deposit Anywhere", "1.2.0")]
    //[BepInProcess("valheim.exe")]
    [HarmonyPatch]
    //Extend BaseUnityPlugin
    public class DepositAnywhere : BaseUnityPlugin
    {
        public static Player _player;
        public static List<Container> containerList = new List<Container>();
        private static ConfigEntry<bool> enableMod;
        private static ConfigEntry<bool> DepositConsumables;
        private static ConfigEntry<bool> DepositAmmo;
        private static ConfigEntry<bool> DepositUtility;
        private static ConfigEntry<bool> DepositMisc;
        public static ConfigEntry<float> range;
        public static ConfigEntry<string> keyDepositString;
        public static ConfigEntry<int> excludedSlots;
        public static KeyCode configDepositKey;

        private static int invSlotCount;
        private static int invWidth;

        void Awake()
        {
            enableMod = Config.Bind("Deposit All Items", "Enable Mod", true, "Enable or disable this mod");
            DepositConsumables = Config.Bind("Deposit All Items", "Deposit Consumables", false, "Whether or not to deposit conumable items");
            DepositAmmo = Config.Bind("Deposit All Items", "Deposit Ammo", false, "Whether or not to deposit ammo");
            DepositUtility = Config.Bind("Deposit All Items", "Deposit Utility", false, "Whether or not to deposit utility items");
            DepositMisc = Config.Bind("Deposit All Items", "Deposit Misc", false, "Whether or not to deposit miscellaneous items");
            range = Config.Bind<float>("Deposit All Items", "Container Range", 10f, "The maximum range to send items");
            keyDepositString = Config.Bind("Deposit All Items", "Deposit All Key", "G", "The key to use to deposit items. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            excludedSlots = Config.Bind("Deposit All Items", "Excluded Slots", 0, "Number of Inventory slots to exclude from depositing.");
            configDepositKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyDepositString.Value);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        public static void PlayerAwake_Patch(Player __instance)
        {
            _player = __instance;
            Inventory inv = __instance.GetInventory();
            int invX = inv.GetWidth();
            int invY = inv.GetHeight();
            invSlotCount = invX * invY;
            invWidth = invX;
        }
        private static Vector2Int ConvertToGrid(int index)
        {
            Vector2Int p = new Vector2Int(0, 0);
            //
            float rowNumber = 1; //the starting row (y)
            float colNumber = 0; //the starting column (x)
            if (index != 0)
            {
                rowNumber = (float)Math.Floor((float)index / 8);
            }
            float subtracter = (float)Math.Floor((float)index / 8);
            colNumber = (((float)index / 8) - subtracter) * 8;

            p.y = (int)rowNumber;
            p.x = (int)colNumber;
            return p;
        }
        static bool BlackListed(ItemDrop.ItemData item)
        {
            if (!DepositConsumables.Value && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable) return true;
            if (!DepositAmmo.Value && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo) return true;
            if (!DepositUtility.Value && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility) return true;
            if (!DepositMisc.Value && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Misc) return true;
            return false;
        }
        static bool AttemptDeposit(ItemDrop.ItemData item, int index, List<Container> boxes, Inventory _inventory)
        {
            //items does not need to be deposited, select next item.
            if (BlackListed(item) || item.m_equiped) return true;
            //item is allowed to be deposited, check surrounding boxes.
            _output += ("===================== ITEM =====================\n");
            _output += ("Name : " + item.m_shared.m_name + " Type : " + item.m_shared.m_itemType + "\n");
            for (var j = 0; j < boxes.Count; j++)
            {
                Inventory boxInventory = boxes[j].GetInventory();
                List<ItemDrop.ItemData> BoxItems = boxInventory.GetAllItems();
                List<string> boxItems = boxInventory.GetAllItems().Select(c => c.m_shared.m_name).ToList();
                if (boxItems.Contains(item.m_shared.m_name))
                {
                    //the box contains the currenty inventory item name
                    if (boxInventory.HaveEmptySlot())
                    {
                        //the box has an empty slot, just deposit the item and break
                        _output += ("Box [" + j + "] has empty slots, Depositing All (" + item.m_stack + ") " + item.m_shared.m_name + "\n");
                        boxInventory.MoveItemToThis(_inventory, item);
                        Traverse.Create(boxInventory).Method("Changed").GetValue();
                        return true;
                    }
                    else
                    {
                        //the box has no empty slot, see if we can fit the item onto existing stacks
                        foreach (ItemDrop.ItemData boxItem in BoxItems)
                        {
                            if (item.m_shared.m_name == boxItem.m_shared.m_name)
                            {
                                //item has the same name as the inventory item, see if we can stack more onto it.
                                int amountToDeposit = HowMuchCanDeposit(boxItem);
                                if (amountToDeposit > 0)
                                {
                                    //we can fit some on this stack, move it.
                                    //if deposit some is true, then there is still some of the item left in player inventory
                                    if (!DepositSome(_inventory, boxInventory, item, boxItem))
                                    {
                                        _output += ("Able to deposit full stack\n");
                                        return true;
                                    }
                                    else
                                    {
                                        _output += ("Couldn't deposit full stack\n");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        static string _output = "";
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        public static void PlayerUpdate_Patch(Player __instance)
        {
            bool keyDown = Input.GetKeyDown(configDepositKey);

            if (keyDown)
            {
                keyDown = false;
                bool success = true;
                _output = "\n";
                GameObject hoverItem = __instance.GetHoverObject();
                if (hoverItem != null)
                {
                    string hoverName = hoverItem.name;
                    if (hoverName.Contains("chest") || hoverName.Contains("Container") || hoverName.Contains("Chest"))
                    {
                        //remove invalid chests
                        containerList = containerList.Where(c => c != null).ToList();
                        List<Container> boxes = GetNearbyContainers(__instance.transform.position);
                        Inventory inventory = __instance.GetInventory();
                        for (var i = invWidth + excludedSlots.Value; i < invSlotCount; i++)
                        {
                            Vector2Int location = ConvertToGrid(i);
                            ItemDrop.ItemData item = inventory.GetItemAt(location.x, location.y);
                            string itemName = item?.m_shared.m_name;
                            if (item != null)
                            {
                                if (!AttemptDeposit(item, i, boxes, inventory))
                                {
                                    success = false;
                                    _output += "Couldn't deposit all " + item.m_shared.m_name + "\n";
                                    _output += "\n";
                                }
                            }
                        }
                        ShowHUDMessage(success ? "Deposited all items" : "Failed to deposit all items");
                    }
                }           
                Debug.Log(_output);
            }
        }
        private static void ShowHUDMessage(string message)
        {
            MessageHud.MessageType ctr = MessageHud.MessageType.Center;
            MessageHud.instance.ShowMessage(ctr, message, 10);
        }
        private static Vector2i GetItemLocationInBox(Inventory boxInventory, ItemDrop.ItemData item)
        {
            int index = boxInventory.GetAllItems().IndexOf(item);
            return new Vector2i(boxInventory.GetItem(index).m_gridPos.x, boxInventory.GetItem(index).m_gridPos.y);
        }
        private static int HowMuchCanDeposit(ItemDrop.ItemData boxItem)
        {
            return boxItem.m_shared.m_maxStackSize - boxItem.m_stack;
        }
        private static bool DepositSome(Inventory playerInventory, Inventory boxInventory, ItemDrop.ItemData inventoryItem, ItemDrop.ItemData boxItem)
        {
            int amountTakeable = boxItem.m_shared.m_maxStackSize - boxItem.m_stack;
            Vector2i location = GetItemLocationInBox(boxInventory, boxItem);
            if (inventoryItem.m_stack > amountTakeable)
            {
                _output += ("Depositing " + amountTakeable + " " + inventoryItem.m_shared.m_name + " At : " + location.ToString() + "\n");
                boxInventory.MoveItemToThis(playerInventory, inventoryItem, amountTakeable, location.x, location.y);
                Traverse.Create(boxInventory).Method("Changed").GetValue();
                return true;
            }
            else
            {
                _output += ("Depositing " + inventoryItem.m_stack + " " + inventoryItem.m_shared.m_name + " At : " + location.ToString() + "\n");
                boxInventory.MoveItemToThis(playerInventory, inventoryItem, inventoryItem.m_stack, location.x, location.y);
                Traverse.Create(boxInventory).Method("Changed").GetValue();
                return false;
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
            static void Postfix(Container __instance)
            {
                if ((__instance.name.Contains("chest") || __instance.name.Contains("Container")) || __instance.name.Contains("Chest"))
                {
                    if (__instance.GetInventory() != null)
                    {
                        containerList.Add(__instance);
                    }
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
        public static string ContainerGetHoverText_Patch(string __result)
        {
            string result = __result;
            result += $"\n[<color=yellow><b>" + keyDepositString.Value + "</b></color>] Deposit All Items";
            return result;
        }
    }
}
