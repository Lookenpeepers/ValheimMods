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

namespace BuildFromBoxes
{
    [BepInPlugin("Lookenpeepers-BuildFromBoxes", "Build From Boxes", "1.0.4")]
    [HarmonyPatch]
    public class BuildFromBoxes : BaseUnityPlugin
    {
        static Player player;
        static List<Container> containerList = new List<Container>();
        private static ConfigEntry<bool> enableMod;
        public static ConfigEntry<string> keyPullString;
        public static KeyCode configPullKey;
        public static ConfigEntry<int> NumSlots;

        private struct ItemToMove
        {
            public ItemToMove(string Name, int Amount)
            {
                name = Name;
                amount = Amount;
            }
            public string name;
            public int amount;
        }

        void Awake()
        {
            enableMod = Config.Bind("2 - Global", "Enable Mod", true, "Enable or disable this mod");
            keyPullString = Config.Bind("1 - Pull Items", "Pull Key", "N", "The key to use to deposit items. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            configPullKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyPullString.Value);
            NumSlots = Config.Bind("1 - Pull Items", "Number of Inventory Slots", 32, "The number of slots in your player inventory(for mods that increase inventory size)");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        public static void PlayerUpdate_Patch(Player __instance)
        {
            bool keyDown = Input.GetKeyDown(configPullKey);

            if (keyDown)
            {
                keyDown = false;
                Piece p = __instance.GetSelectedPiece();
                if (p != null)
                {
                    RemoveInvalidChests(p);
                    //GetResourcesForPull(p);
                }
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        private static void PlayerAwake_Patch(Player __instance)
        {
            player = __instance;
        }
        private static List<ItemToMove> GetNeededItems(Piece p)
        {
            List<ItemToMove> neededItems = new List<ItemToMove>();

            List<ItemDrop.ItemData> playerItems = new List<ItemDrop.ItemData>();
            playerItems.AddRange(player.GetInventory().GetAllItems().ToArray());
            string[] strPlayerItems = playerItems.Select(itm => itm.m_shared.m_name).Distinct().ToArray();

            foreach (Piece.Requirement pr in p.m_resources)
            {
                ItemDrop.ItemData reqItem = pr.m_resItem.m_itemData;
                int amountNeeded = pr.m_amount;

                if (strPlayerItems.Contains(reqItem.m_shared.m_name))
                {
                    foreach (ItemDrop.ItemData invItem in playerItems)
                    {
                        if (invItem.m_shared.m_name == reqItem.m_shared.m_name)
                        {
                            int invItemAmount = invItem.m_stack;
                            if (invItemAmount >= amountNeeded)
                            {
                                //we have enough, subtract from amount needed and break item loop
                                amountNeeded = 0;
                                break;
                            }
                            else
                            {
                                amountNeeded -= invItemAmount;
                            }
                        }
                    }
                    if (amountNeeded > 0)
                    {
                        ItemToMove toAdd = new ItemToMove(reqItem.m_shared.m_name, amountNeeded);
                        neededItems.Add(toAdd);
                        Debug.Log("Need " + amountNeeded + " " + reqItem.m_shared.m_name);
                    }
                }
                else
                {
                    //doesn't exist
                    ItemToMove toAdd = new ItemToMove(reqItem.m_shared.m_name, amountNeeded);
                    neededItems.Add(toAdd);
                    Debug.Log("Need " + amountNeeded + " " + reqItem.m_shared.m_name);
                }
            }
            return neededItems;
        }
        private static bool DoAllResourcesExist(List<ItemToMove> p)
        {
            bool HaveAll = true;
            foreach (ItemToMove pr in p)
            {
                //ItemDrop.ItemData reqItem = pr.m_resItem.m_itemData;
                string reqName = pr.name + " : " + pr.amount;
                int amountNeeded = pr.amount;
                //Debug.Log(reqName);
                foreach (Container c in containerList)
                {
                    List<ItemDrop.ItemData> BoxItems = new List<ItemDrop.ItemData>();
                    BoxItems.AddRange(c.GetInventory().GetAllItems().ToArray());
                    string[] strBoxItems = BoxItems.Select(itm => itm.m_shared.m_name).Distinct().ToArray();
                    if (strBoxItems.Contains(pr.name))
                    {
                        //Debug.Log("Found " + pr.name);
                        //found item, see if there's enough
                        foreach (ItemDrop.ItemData boxItem in BoxItems)
                        {
                            if (boxItem.m_shared.m_name == pr.name)
                            {
                                int boxItemAmount = boxItem.m_stack;
                                if (boxItemAmount >= amountNeeded)
                                {
                                    //we have enough, subtract from amount needed and break item loop
                                    amountNeeded = 0;
                                    break;
                                }
                                else
                                {
                                    amountNeeded -= boxItemAmount;
                                }
                            }
                        }
                        //break;
                    }
                    if (amountNeeded == 0)
                    {
                        break;
                    }
                }
                if (amountNeeded > 0)
                {
                    HaveAll = false;
                }
            }
            return HaveAll;
        }
        private static void DoItAll(List<ItemToMove> p)
        {
            List<ItemDrop.ItemData> playerItems = new List<ItemDrop.ItemData>();
            playerItems.AddRange(player.GetInventory().GetAllItems().ToArray());
            string[] strPlayerItems = playerItems.Select(itm => itm.m_shared.m_name).Distinct().ToArray();

            foreach (ItemToMove pr in p)
            {
                //ItemDrop.ItemData reqItem = pr.m_resItem.m_itemData;
                string reqName = pr.name + " : " + pr.amount;
                int amountNeeded = pr.amount;
                Debug.Log(reqName);
                Vector2Int openSlot = GetOpenInvSlot();

                foreach (Container c in containerList)
                {
                    List<ItemDrop.ItemData> BoxItems = new List<ItemDrop.ItemData>();
                    BoxItems.AddRange(c.GetInventory().GetAllItems().ToArray());
                    string[] strBoxItems = BoxItems.Select(itm => itm.m_shared.m_name).Distinct().ToArray();


                    if (strBoxItems.Contains(pr.name))
                    {
                        Debug.Log("Found " + pr.name);
                        //found out item, see if it's enough
                        foreach (ItemDrop.ItemData boxItem in BoxItems)
                        {
                            if (boxItem.m_shared.m_name == pr.name)
                            {
                                int boxItemAmount = boxItem.m_stack;
                                if (boxItemAmount >= amountNeeded)
                                {
                                    if (amountNeeded > 0)
                                    {
                                        //still need it, check for open slots

                                        player.GetInventory().MoveItemToThis(c.GetInventory(), boxItem, amountNeeded, openSlot.x, openSlot.y);
                                        amountNeeded = 0;
                                        break;
                                    }
                                    else
                                    {
                                        //obtained all of this item, break boxitem loop
                                        break;
                                    }
                                }
                                else
                                {
                                    player.GetInventory().MoveItemToThis(c.GetInventory(), boxItem, amountNeeded, openSlot.x, openSlot.y);
                                    amountNeeded -= boxItemAmount;
                                }
                            }
                        }
                        if (amountNeeded == 0)
                        {
                            break;
                        }
                    }
                }
            }
            ShowHUDMessage("Pulled items");
        }
        private static void RemoveInvalidChests(Piece p)
        {
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
            List<ItemToMove> neededItems = GetNeededItems(p);
            //GET EMPTY SLOTS DIFFERENTLY
            Debug.Log("Free slots : " + player.GetInventory().GetEmptySlots());
            if (neededItems.Count > 0 && neededItems.Count <= player.GetInventory().GetEmptySlots())
            {
                //we need items, check if we have the resources around us.
                if (DoAllResourcesExist(neededItems))
                {
                    //we have the resouces, pull what we need
                    Debug.Log("Have all resources");
                    DoItAll(neededItems);
                }
            }
            else if (neededItems.Count > 0 && neededItems.Count > player.GetInventory().GetEmptySlots())
            {
                Debug.Log("Not enough inventory space");
                ShowHUDMessage("Not enough inventory space");
            }
            else if (neededItems.Count == 0)
            {
                Debug.Log("Inventory contains all required items.");
                ShowHUDMessage("Inventory contains all items");
            }
            //if (DoAllResourcesExist(p))
            //{
            //    DoItAll(p);
            //}
        }
        private static void ShowHUDMessage(string message)
        {
            MessageHud.MessageType ctr = MessageHud.MessageType.Center;
            MessageHud.instance.ShowMessage(ctr, message, 10);
        }
        private static bool InvContainsItem(Piece.Requirement pr)
        {
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                string cleanName = item.m_shared.m_name.Replace("$item_", "");
                if (pr.m_resItem.name.ToLower() == cleanName)
                {
                    //check if it's the right amount
                    if (item.m_stack >= pr.m_amount)
                    {
                        //success
                        return true;
                    }
                }
            }
            return false;
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
        private static Vector2Int GetOpenInvSlot()
        {
            Vector2Int tmp = new Vector2Int(-1, -1);

            for (var i = 0; i < NumSlots.Value; i++)
            {
                Vector2Int toGrid = ConvertToGrid(i);
                ItemDrop.ItemData tmpItem = player.GetInventory().GetItemAt(toGrid.x, toGrid.y);
                if (tmpItem == null && !unusables.Contains(new Vector2Int(toGrid.x, toGrid.y)))
                {
                    tmp = new Vector2Int(toGrid.x, toGrid.y);
                    return tmp;
                }
            }
            return tmp;
        }
        static List<Vector2Int> unusables = new List<Vector2Int>();

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
    }
}
