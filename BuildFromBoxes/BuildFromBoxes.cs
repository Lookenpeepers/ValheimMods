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
    [BepInPlugin("Lookenpeepers-BuildFromBoxes", "Build From Boxes", "1.0.2")]
    [HarmonyPatch]
    public class BuildFromBoxes : BaseUnityPlugin
    {
        static Player player;
        static List<Container> containerList = new List<Container>();
        private static ConfigEntry<bool> enableMod;
        static List<ItemToMove> itemsToMove;
        public static ConfigEntry<string> keyPullString;
        public static KeyCode configPullKey;

        private struct StrInt
        {
            public string name;
            public int amount;
        }
        private struct ItemToMove
        {
            public Container source;
            public ItemDrop.ItemData item;
            public int amount;
            public Vector2i destinationPos;
        }

        void Awake()
        {
            enableMod = Config.Bind("2 - Global", "Enable Mod", true, "Enable or disable this mod");
            keyPullString = Config.Bind("1 - Pull Items", "Pull Key", "N", "The key to use to deposit items. KeyCodes can be found here https://docs.unity3d.com/ScriptReference/KeyCode.html");
            configPullKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyPullString.Value);
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
                    GetResourcesForPull(p);
                }                
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        private static void PlayerAwake_Patch(Player __instance)
        {
            player = __instance;
        }

        private static void GetResourcesForPull(Piece piece)
        {
            List<int> deletables = new List<int>();
            itemsToMove = new List<ItemToMove>();
            unusables = new List<Vector2i>();
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

            string output = "\nPiece : " + piece.m_name;

            List<string> reqItems = new List<string>();
            foreach (Piece.Requirement r in piece.m_resources)
            {
                reqItems.Add(r.m_resItem.name);
                output += "\n" + r.m_resItem.name + " : " + r.m_amount;
            }

            List<StrInt> _totals = new List<StrInt>();

            foreach (Piece.Requirement r in piece.m_resources)
            {
                StrInt _tmp = new StrInt();
                _tmp.name = r.m_resItem.name;
                _tmp.amount = 0;
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                {
                    string cleanName = item.m_shared.m_name.Replace("$item_", "");
                    if (cleanName == r.m_resItem.name.ToLower())
                    {
                        _tmp.amount += item.m_stack;
                    }
                }
                foreach (Container c in containerList)
                {
                    if (!c.IsInUse())
                    {
                        List<ItemDrop.ItemData> tmpItems = c.GetInventory().GetAllItems();
                        foreach (ItemDrop.ItemData item in tmpItems)
                        {
                            string cleanName = item.m_shared.m_name.Replace("$item_", "");
                            if (cleanName == r.m_resItem.name.ToLower())
                            {
                                _tmp.amount += item.m_stack;
                            }
                        }
                    }
                }
                output += "\n" + _tmp.name + " Total : " + _tmp.amount;
                _totals.Add(_tmp);
            }
            if (!DoesInventoryContainAllItems(piece))
            {
                if (DoesContainAllResources(_totals, piece))
                {
                    for (var i = 0; i < _totals.Count; i++)
                    {
                        //have all items, check if we have enough inventory space and pull.
                        ItemDrop.ItemData testItem = player.GetInventory().GetItem("$item_" + _totals[i].name.ToLower());
                        if (testItem != null)
                        {
                            //item already exists, subtract it from the total needed
                            Debug.Log("Pulling Items already contained");
                            PullItems(piece.m_resources.ElementAt(i).m_amount - testItem.m_stack, "$item_" + _totals[i].name.ToLower(), true);
                        }
                        else if (player.GetInventory().GetEmptySlots() >= piece.m_resources.Length)
                        {
                            Debug.Log("Pulling Items");
                            PullItems(piece.m_resources.ElementAt(i).m_amount, "$item_" + _totals[i].name.ToLower());
                        }
                        else
                        {
                            Debug.Log("Not enough inventory space to pull items.");
                            //debug
                        }
                    }
                    ShowHUDMessage("Items pulled");
                }
                else
                {
                    ShowHUDMessage("Not enough resources in range");
                    Debug.Log("Not enough Resources in range.");
                }
            }
            else
            {
                ShowHUDMessage("Inventory Contains All Required Items");
            }
            Debug.Log(output);
        }
        private static void ShowHUDMessage(string message)
        {
            MessageHud.MessageType ctr = MessageHud.MessageType.Center;
            MessageHud.instance.ShowMessage(ctr, message, 10);
        }
        private static bool DoesContainAllResources(List<StrInt> _totals, Piece piece)
        {
            for (var i = 0; i < _totals.Count; i++)
            {
                if (_totals[i].amount < piece.m_resources.ElementAt(i).m_amount)
                {
                    //not enough items.
                    return false;
                }
            }
            return true;
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
        private static bool DoesInventoryContainAllItems(Piece piece)
        {
            foreach (Piece.Requirement pr in piece.m_resources)
            {
                ItemDrop.ItemData neededItem = pr.m_resItem.m_itemData;
                if (!InvContainsItem(pr))
                {
                    return false;
                }
            }
            return true;
        }
        private static Vector2i GetOpenInvSlot()
        {
            Vector2i tmp = new Vector2i(0, 0);
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    if (player.GetInventory().GetItemAt(x, y) == null && !unusables.Contains(new Vector2i(x, y)))
                    {
                        tmp = new Vector2i(x, y);
                        return tmp;
                    }
                }
            }
            return tmp;
        }
        private static Vector2i GetExistingItem(ItemDrop.ItemData item, int amount)
        {
            Vector2i tmp = new Vector2i(0, 0);
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    if (player.GetInventory().GetItemAt(x, y)?.m_shared.m_name == item.m_shared.m_name)
                    {
                        if (player.GetInventory().GetItemAt(x, y).m_stack <= item.m_shared.m_maxStackSize - amount)
                        {
                            tmp = new Vector2i(x, y);
                            return tmp;
                        }
                    }
                }
            }
            return tmp;
        }
        //finish pull process
        static void FinishPull()
        {
            foreach (ItemToMove itm in itemsToMove)
            {
                player.GetInventory().MoveItemToThis(itm.source.GetInventory(), itm.item, itm.amount, itm.destinationPos.x, itm.destinationPos.y);
            }
        }
        //Pull from containers
        static List<Vector2i> unusables = new List<Vector2i>();
        static void PullItems(int amount, string itemName, bool alreadyHave = false)
        {
            bool itemRetrieved = false;
            Vector2i stillStacking = new Vector2i(-1, -1);
            foreach (Container c in containerList)
            {
                if (!itemRetrieved)
                {
                    foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems())
                    {
                        Vector2i itemPos = item.m_gridPos;
                        if (item.m_shared.m_name == itemName && item.m_stack >= amount)
                        {

                            if (alreadyHave)
                            {
                                if (stillStacking.x != -1)
                                {
                                    Debug.Log("Rest of " + item.m_shared.m_name + " in one box");
                                    Debug.Log(itemPos.ToString());
                                    Vector2i tmp = stillStacking;
                                    //mark item for moving, don't move it here.
                                    ItemToMove itm = new ItemToMove();
                                    itm.source = c;
                                    itm.item = item;
                                    itm.amount = amount;
                                    itm.destinationPos = tmp;
                                    unusables.Add(tmp);
                                    itemsToMove.Add(itm);
                                    //player.GetInventory().MoveItemToThis(c.GetInventory(), item, amount, tmp.x, tmp.y);
                                    //stillStacking = new Vector2i(-1, -1);
                                    itemRetrieved = true;
                                    break;
                                }
                                else
                                {
                                    Debug.Log("All " + item.m_shared.m_name + " in one box");
                                    Debug.Log(itemPos.ToString());
                                    Vector2i tmp = GetExistingItem(item, amount);
                                    //mark item for moving, don't move it here.
                                    ItemToMove itm = new ItemToMove();
                                    itm.source = c;
                                    itm.item = item;
                                    itm.amount = amount;
                                    itm.destinationPos = tmp;
                                    unusables.Add(tmp);
                                    itemsToMove.Add(itm);
                                    //get open slots
                                    //player.GetInventory().MoveItemToThis(c.GetInventory(), item, amount, tmp.x, tmp.y);
                                    itemRetrieved = true;
                                    break;
                                }
                            }
                            else
                            {
                                if (stillStacking.x != -1)
                                {
                                    Debug.Log("Rest of " + item.m_shared.m_name + " in one box");
                                    Debug.Log(itemPos.ToString());
                                    Vector2i tmp = stillStacking;
                                    //mark item for moving, don't move it here.
                                    ItemToMove itm = new ItemToMove();
                                    itm.source = c;
                                    itm.item = item;
                                    itm.amount = amount;
                                    itm.destinationPos = tmp;
                                    unusables.Add(tmp);
                                    itemsToMove.Add(itm);
                                    //player.GetInventory().MoveItemToThis(c.GetInventory(), item, amount, tmp.x, tmp.y);
                                    //stillStacking = new Vector2i(-1, -1);
                                    itemRetrieved = true;
                                    break;
                                }
                                else
                                {
                                    Debug.Log("All " + item.m_shared.m_name + " in one box");
                                    Debug.Log(itemPos.ToString());
                                    Vector2i tmp = GetOpenInvSlot();
                                    //mark item for moving, don't move it here.
                                    ItemToMove itm = new ItemToMove();
                                    itm.source = c;
                                    itm.item = item;
                                    itm.amount = amount;
                                    itm.destinationPos = tmp;
                                    unusables.Add(tmp);
                                    itemsToMove.Add(itm);
                                    //get open slots
                                    //player.GetInventory().MoveItemToThis(c.GetInventory(), item, amount, tmp.x, tmp.y);
                                    itemRetrieved = true;
                                    break;
                                }
                            }
                        }
                        if (item.m_shared.m_name == itemName && item.m_stack < amount)
                        {
                            if (alreadyHave)
                            {
                                Debug.Log("Some " + item.m_shared.m_name + " in one box");

                                stillStacking = GetExistingItem(item, amount);
                                //mark item for moving, don't move it here.
                                ItemToMove itm = new ItemToMove();
                                itm.source = c;
                                itm.item = item;
                                itm.amount = item.m_stack;
                                itm.destinationPos = stillStacking;
                                itemsToMove.Add(itm);
                                amount -= item.m_stack;
                            }
                            else
                            {
                                Debug.Log("Some " + item.m_shared.m_name + " in one box");
                                stillStacking = GetOpenInvSlot();
                                //mark item for moving, don't move it here.
                                ItemToMove itm = new ItemToMove();
                                itm.source = c;
                                itm.item = item;
                                itm.amount = item.m_stack;
                                itm.destinationPos = stillStacking;
                                itemsToMove.Add(itm);
                                amount -= item.m_stack;
                            }
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            FinishPull();
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
    }
}
