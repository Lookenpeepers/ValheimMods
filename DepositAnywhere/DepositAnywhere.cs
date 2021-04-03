using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DepositAnywhere
{
    //Initialize BepInEx
    [BepInPlugin("Lookenpeepers-DepositAnywhere", "Deposit Anywhere", "1.1.3")]
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
        public static ConfigEntry<int> NumberOfInventorySlots;
        public static KeyCode configDepositKey;

        void Awake()
        {
            enableMod = Config.Bind("2 - Global", "Enable Mod", true, "Enable or disable this mod");
            range = Config.Bind<float>("3 - General", "ContainerRange", 10f, "The maximum range to send items");
            keyDepositString = Config.Bind("1 - Deposit All Items", "Deposit All Key", "G", "The key to use to deposit items. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            excludedSlots = Config.Bind("1 - Deposit All Items", "Excluded Slots", 0, "Number of Inventory slots to exclude from depositing.");
            NumberOfInventorySlots = Config.Bind("1 - Deposit All Items", "Number of Inventory Slots", 32, "How many inventory slots. (to work with mods that increase inventory slots)");
            configDepositKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyDepositString.Value);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
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
        static string _output = "";
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        public static void PlayerUpdate_Patch(Player __instance)
        {
            bool keyDown = Input.GetKeyDown(configDepositKey);

            if (keyDown)
            {
                _output = "\n";
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
                bool success = true;
                for (var i = 8 + excludedSlots.Value; i < NumberOfInventorySlots.Value; i++)
                {
                    Vector2Int location = ConvertToGrid(i);
                    ItemDrop.ItemData item = inventory.GetItemAt(location.x, location.y);
                    string itemName = item?.m_shared.m_name;
                    //loop through each chest to find a matching item
                    if (item != null && !item.m_equiped)
                    {
                        _output += ("======= ITEM =======\n");
                        _output += ("Name : " + item.m_shared.m_name + "\n");
                        bool deposited = false;
                        for (var j = 0; j < boxes.Count; j++)
                        {
                            Inventory boxInventory = boxes[j].GetInventory();
                            //Debug.Log("max x : " + boxInventory.GetWidth() + " max y : " + boxInventory.GetHeight());
                            List<ItemDrop.ItemData> BoxItems = new List<ItemDrop.ItemData>();
                            List<string> boxItems = new List<string>();
                            //generate list of items in box
                            foreach (ItemDrop.ItemData boxItem in boxInventory.GetAllItems())
                            {
                                //boxItems.Add(boxItem?.m_shared.m_name);
                                BoxItems.Add(boxItem);
                                boxItems.Add(boxItem.m_shared.m_name);
                            }

                            if (boxItems.Contains(item.m_shared.m_name))
                            {
                                //The item is in this box, check if we can deposit
                                if (boxInventory.HaveEmptySlot())
                                {
                                    //the box has empty slots, can deposit item 100%
                                    _output += ("Box [" + j + "] has empty slots, Depositing All (" + item.m_stack + ") " + item.m_shared.m_name + "\n");
                                    boxInventory.MoveItemToThis(inventory, item);
                                    deposited = true;
                                    break;
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
                                                //_output += ("Can deposit " + boxItem.m_shared.m_name + "\n");
                                                //we can fit some on this stack, move it.
                                                //if deposit some is true, then there is still some of the item left in player inventory
                                                if (!DepositSome(inventory, boxInventory, item, boxItem))
                                                {
                                                    _output += ("Able to deposit full stack\n");
                                                    deposited = true;
                                                    break;
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
                            if (deposited)
                            {
                                break;
                            }
                        }
                        if (deposited)
                        {
                            _output += ("Successfully deposited " + item.m_shared.m_name + "\n");
                        }
                        else
                        {
                            _output += ("Couldn't deposit all " + item.m_shared.m_name + "\n");
                            success = false;
                        }
                    }
                }
                if (success)
                {
                    ShowHUDMessage("Deposited all items");
                }
                else
                {
                    ShowHUDMessage("Failed to deposit all items");
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
            Vector2i loc = new Vector2i(boxInventory.GetItem(index).m_gridPos.x, boxInventory.GetItem(index).m_gridPos.y);

            return loc;
        }
        private static int HowMuchCanDeposit(ItemDrop.ItemData boxItem)
        {
            int amountTakeable = boxItem.m_shared.m_maxStackSize - boxItem.m_stack;
            return amountTakeable;
        }
        private static bool DepositSome(Inventory playerInventory, Inventory boxInventory, ItemDrop.ItemData inventoryItem, ItemDrop.ItemData boxItem)
        {
            int amountTakeable = boxItem.m_shared.m_maxStackSize - boxItem.m_stack;
            Vector2i location = GetItemLocationInBox(boxInventory, boxItem);
            if (inventoryItem.m_stack > amountTakeable)
            {
                _output += ("Depositing " + amountTakeable + " " + inventoryItem.m_shared.m_name + " At : " + location.ToString() + "\n");
                boxInventory.MoveItemToThis(playerInventory, inventoryItem, amountTakeable, location.x, location.y);
                return true;
            }
            else
            {
                _output += ("Depositing " + inventoryItem.m_stack + " " + inventoryItem.m_shared.m_name + " At : " + location.ToString() + "\n");
                boxInventory.MoveItemToThis(playerInventory, inventoryItem, inventoryItem.m_stack, location.x, location.y);
                return false;
            }
        }
        private static bool CanDepositAll(ItemDrop.ItemData inventoryItem, ItemDrop.ItemData boxItem)
        {
            int invItemAmount = inventoryItem.m_stack;
            int boxItemAmount = boxItem.m_stack;
            if (invItemAmount + boxItemAmount <= boxItem.m_shared.m_maxStackSize)
            {
                return true;
            }
            else
            {
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
