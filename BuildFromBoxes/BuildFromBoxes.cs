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
    [BepInPlugin("Lookenpeepers-BuildFromBoxes", "Build From Boxes", "1.0.6")]
    [HarmonyPatch]
    public class BuildFromBoxes : BaseUnityPlugin
    {
        static Player player;
        static List<Container> containerList = new List<Container>();
        private static ConfigEntry<bool> enableMod;
        public static ConfigEntry<string> keyPullString;
        public static KeyCode configPullKey;

        private static int invSlotCount;
        private static int invWidth;
        private static int invHeight;

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
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private static bool CheckValidity(Container c)
        {
            if (c.GetInventory() != null)
            {
                if ((c.name.Contains("chest") || c.name.Contains("Container")))
                {
                    long ID = player.GetPlayerID();
                    if (Traverse.Create(c).Method("CheckAccess", new object[] { ID }).GetValue<bool>())
                    {
                        Traverse.Create(c).Method("Load").GetValue();
                        return true;
                    }
                }
            }
            return false;
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
                    _output = "\n";
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
            int x = __instance.GetInventory().GetWidth();
            int y = __instance.GetInventory().GetHeight();
            invSlotCount = x * y;
            invWidth = x;
            invHeight = y;
        }
        static string _output = "";
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
                        _output += ("Need " + amountNeeded + " " + reqItem.m_shared.m_name + "\n");
                    }
                }
                else
                {
                    //doesn't exist
                    ItemToMove toAdd = new ItemToMove(reqItem.m_shared.m_name, amountNeeded);
                    neededItems.Add(toAdd);
                    _output += ("Need " + amountNeeded + " " + reqItem.m_shared.m_name + "\n");
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
            _output += "Need " + p.Count + " items\n";
            foreach (ItemToMove pr in p)
            {
                //ItemDrop.ItemData reqItem = pr.m_resItem.m_itemData;
                string reqName = pr.name + " : " + pr.amount;
                int amountNeeded = pr.amount;
                Debug.Log(reqName);
                Vector2Int openSlot = GetOpenInvSlot();

                foreach (Container c in containerList)
                {
                    if (amountNeeded > 0)
                    {
                        List<ItemDrop.ItemData> BoxItems = new List<ItemDrop.ItemData>();
                        BoxItems.AddRange(c.GetInventory().GetAllItems().ToArray());
                        string[] strBoxItems = BoxItems.Select(itm => itm.m_shared.m_name).Distinct().ToArray();

                        if (strBoxItems.Contains(pr.name))
                        {
                            _output += ("Found " + pr.name + "\n");
                            //found item, see if it's enough
                            if (amountNeeded > 0)
                            {
                                //still need some, loop through box items
                                foreach (ItemDrop.ItemData boxItem in BoxItems)
                                {
                                    if (boxItem.m_shared.m_name == pr.name)
                                    {
                                        //if the player inventory contains the needed item already, just move it.
                                        //find item in inventory and return the gridposition
                                        Vector2Int existingItem = GetExistingItemLocation(pr.name);
                                        if (existingItem.x != -1)
                                        {
                                            //the item exists in the players inventory, pull the rest needed
                                            int boxItemAmount = boxItem.m_stack;
                                            if (boxItemAmount >= amountNeeded)
                                            {
                                                player.GetInventory().MoveItemToThis(c.GetInventory(), boxItem, amountNeeded, existingItem.x, existingItem.y);
                                                _output += "Pulled All " + boxItem.m_shared.m_name + " to existing stack, No more needed\n";
                                                amountNeeded = 0;
                                                break;
                                            }
                                            else
                                            {
                                                player.GetInventory().MoveItemToThis(c.GetInventory(), boxItem, boxItemAmount, existingItem.x, existingItem.y);
                                                amountNeeded -= boxItemAmount;
                                                _output += "Pulled " + boxItemAmount + " " + boxItem.m_shared.m_name + " Still need " + amountNeeded + "\n";
                                            }
                                        }
                                        else
                                        {
                                            int boxItemAmount = boxItem.m_stack;
                                            if (boxItemAmount >= amountNeeded)
                                            {
                                                player.GetInventory().MoveItemToThis(c.GetInventory(), boxItem, amountNeeded, openSlot.x, openSlot.y);
                                                _output += "Pulled " + amountNeeded + " " + boxItem.m_shared.m_name + " No more needed\n";
                                                amountNeeded = 0;
                                                break;
                                            }
                                            else
                                            {
                                                player.GetInventory().MoveItemToThis(c.GetInventory(), boxItem, boxItemAmount, openSlot.x, openSlot.y);
                                                amountNeeded -= boxItemAmount;
                                                _output += "Pulled " + boxItemAmount + " " + boxItem.m_shared.m_name + " Still need " + amountNeeded + "\n";
                                            }
                                        }                                        
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //no more needed, break container loop.
                        break;
                    }
                }
            }
            Debug.Log(_output);
            ShowHUDMessage("Pulled items");
        }
        private static Vector2Int GetExistingItemLocation(string name)
        {
            Vector2Int result = new Vector2Int(-1, -1);
            for (var i = 0; i < invSlotCount; i++)
            {
                Vector2Int curLoc = ConvertToGrid(i);
                ItemDrop.ItemData item = player.GetInventory().GetItemAt(curLoc.x, curLoc.y);
                if (item != null)
                {
                    if (item.m_shared.m_name == name)
                    {
                        result = curLoc;
                    }
                }
            }
                return result;
        }
        private static void RemoveInvalidChests(Piece p)
        {
            containerList = containerList.Where(box => box != null).ToList();
            foreach (Container c in containerList)
            {
                Traverse.Create(c).Method("Load").GetValue();
            }
            List<ItemToMove> neededItems = GetNeededItems(p);
            //GET EMPTY SLOTS DIFFERENTLY
            int emptySlots = GetEmptySpaces();
            _output += ("Free slots : " + emptySlots + "\n");
            if (neededItems.Count > 0 && neededItems.Count <= emptySlots)
            {
                //we need items, check if we have the resources around us.
                if (DoAllResourcesExist(neededItems))
                {
                    //we have the resouces, pull what we need
                    _output += ("Have all resources\n");
                    DoItAll(neededItems);
                }
                else
                {
                    _output += "Not enough resources in range\n";
                    ShowHUDMessage("Not enough resources in range");
                    Debug.Log(_output);
                }
            }
            else if (neededItems.Count > 0 && neededItems.Count > emptySlots)
            {
                //check if the needed item can stack into the player inventory
                bool canPull = true;
                foreach(ItemToMove neededitem in neededItems)
                {
                    if (CanItemStack(neededitem.name,neededitem.amount))
                    {
                        //item can stack, keep going
                    }
                    else
                    {
                        canPull = false;
                        break;
                    }
                }
                if (canPull)
                {
                    _output += ("stacking item with current stack\n");
                    DoItAll(neededItems);
                    //ShowHUDMessage("Not enough inventory space");
                }
                else
                {
                    _output += ("Not enough inventory space\n");
                    ShowHUDMessage("Not enough inventory space");
                    Debug.Log(_output);
                }                
            }
            else if (neededItems.Count == 0)
            {
                _output += ("Inventory contains all required items.\n");
                ShowHUDMessage("Inventory contains all items");
                Debug.Log(_output);
            }
        }
        private static bool CanItemStack(string name, int amount)
        {
            bool canStack = false;
            int amountToFit = amount;
            for (var x = 0; x < invWidth; x++)
            {
                for(var y = 0; y <invHeight; y++)
                {
                    ItemDrop.ItemData tmpItem = player.GetInventory().GetItemAt(x, y);
                    if (tmpItem.m_shared.m_name == name)
                    {
                        //check if we can stack it
                        int canFit = tmpItem.m_shared.m_maxStackSize - tmpItem.m_stack;
                        if (canFit >= amountToFit)
                        {
                            //it can fit.
                            canStack = true;
                            break;
                        }
                    }
                }
                if (canStack)
                {
                    break;
                }
            }
            return canStack;
        }
        private static void ShowHUDMessage(string message)
        {
            MessageHud.MessageType ctr = MessageHud.MessageType.Center;
            MessageHud.instance.ShowMessage(ctr, message, 10);
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
        private static int GetEmptySpaces()
        {
            int result = 0;
            for (var x = 0; x < invWidth; x++)
            {
                for (var y = 0; y < invHeight; y++)
                {
                    if (player.GetInventory().GetItemAt(x,y) == null)
                    {
                        result += 1;
                    }                   
                }
            }
            return result;
        }
        private static Vector2Int GetOpenInvSlot()
        {
            Vector2Int tmp = new Vector2Int(-1, -1);

            for (var i = 0; i < invSlotCount; i++)
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
            static void Postfix(Container __instance)
            {
                if (CheckValidity(__instance))
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
