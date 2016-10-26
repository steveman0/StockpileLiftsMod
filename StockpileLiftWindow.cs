using System.Collections.Generic;
using UnityEngine;
using FortressCraft.Community.Utilities;
using System.IO;

public class StockpileLiftControllerWindow : BaseMachineWindow
{
    public const string InterfaceName = "steveman0.StockpileLiftControllerWindow";
    public const string InterfaceSwapCargoLift = "SwapCargoLift";
    public const string InterfaceToggleLoadMode = "ToggleLoadMode";
    public const string InterfaceToggleLoadOrder = "ToggleLoadOrder";
    public const string InterfaceResetRailCheck = "ResetRailCheck";
    public const string InterfaceSetLiftStock = "SetLiftStock";
    public const string InterfaceRegisterLiftStock = "RegisterLiftStock";
    public const string IconLift = "icon_lift";
    public static bool dirty;
    public static bool networkRedraw;
    private string errorString;
    private StockpileLiftController.OperatingState lastState;
    private bool registerstock = false;
    private bool ItemSearch = false;
    private List<ItemBase> SearchResults;
    private int Counter;
    private string EntryString;

    public override void SpawnWindow(SegmentEntity targetEntity)
    {
        StockpileLiftController controller = (targetEntity as StockpileLiftController);
        //Catch for when the window is called on an inappropriate machine
        if (controller == null)
        {
            //GenericMachinePanelScript.instance.Hide();
            UIManager.RemoveUIRules("Machine");
            return;
        }
        //UIUtil.UIdelay = 0;
        //UIUtil.UILock = true;


        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
        this.manager.SetTitle("Stockpile Lift Controller");
        UIManager.instance.ShowInventoryPanel();

        if (!this.registerstock && !this.ItemSearch)
        {
            this.manager.AddPowerBar("power", 80, 0);
            if (center == null)
                Debug.Log("Center block of Stockpile lift controller is null!");
            this.manager.AddBigLabel("rail_state", center.mRailState.ToString(), Color.white, 0, 70);
            this.manager.AddButton("rail_check", "Check Rail", 200, 70);
            this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "rail_depth", "Shaft depth: " + center.mnRailDepth.ToString("N0"), Color.white, false, 0, 100);
            this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "rail_check_depth", string.Empty, Color.white, false, 200, 100);
            this.manager.AddIcon("icon_lift", "empty", Color.white, 0, 140);
            this.manager.AddBigLabel("icon_lift_name", center.mLiftItem != null ? ItemManager.GetItemName(center.mLiftItem) : "No lift deployed", Color.yellow, 60, 145);
            if (center.mLiftItem != null)
            {
                this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "lift_state", "State: ", Color.white, false, 0, 190);
                this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "lift_order", "Order: ", Color.white, false, 0, 210);
                this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "lift_depth", "Depth: ", Color.white, false, 0, 230);
                this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "lift_speed", "Speed: ", Color.white, false, 0, 250);
                this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "lift_cargo", "Cargo: ", Color.white, false, 0, 270);
            }
            this.manager.AddLabel(GenericMachineManager.LabelType.OneLineFullWidth, "error_label", string.Empty, Color.red, false, 0, 300);
            this.manager.AddButton("registerstocklimits", "Set Stock", 200, 300);
            this.manager.AddBigLabel("order_label", "Orders", Color.white, 0, 345);
            this.manager.AddButton("order_switch", "Switch", 200, 345);
            this.manager.AddBigLabel("order_top", "Top: " + center.mUpperLoadOrder.ToString(), Color.white, 0, 390);
            this.manager.AddButton("order_top_toggle", "Toggle", 200, 390);
            this.manager.AddBigLabel("order_bottom", "Bottom: " + center.mLowerLoadOrder.ToString(), Color.white, 0, 435);
            this.manager.AddButton("order_bottom_toggle", "Toggle", 200, 435);
        }
        else if (this.registerstock && !this.ItemSearch)
        {
            this.manager.SetTitle("Stockpile Lift Controller");
            this.manager.AddButton("stockcancel", "Done", 100, 0);

            int spacing = 125;
            int count = 0;
            int offset = 50;
            if (center.mLiftMob != null)
                count = center.mLiftMob.StockLimits.Count;
            for (int n = 0; n < count + 1; n++)
            {
                int suffix = n;
                if (n == count)
                    suffix = -1;
                this.manager.AddIcon("stockitem" + suffix, "empty", Color.white, 0, offset + (spacing * n));
                this.manager.AddBigLabel("stocktitle" + suffix, "Add New Restricted Item", Color.white, 60, offset + (spacing * n));
                if (suffix != -1)
                {
                    //this.manager.AddLabel(GenericMachineManager.LabelType.OneLineHalfWidth, "limittitle" + n, "Inventory Limit", Color.white, false, 75, offset + (spacing * n + 40));
                    this.manager.AddBigLabel("stocklimit" + n, "Inventory Limit", Color.white, 260, offset + (spacing * n));
                    this.manager.AddButton("decreasestock" + n, "Decrease Stock", 25, offset + (spacing * n + 60));
                    this.manager.AddButton("increasestock" + n, "Increase Stock", 175, offset + (spacing * n + 60));
                }
            }
        }
        else if (this.ItemSearch)
        {
            this.manager.AddButton("searchcancel", "Cancel", 100, 0);
            this.manager.AddBigLabel("searchtitle", "Enter Item Search Term", Color.white, 50, 40);
            this.manager.AddBigLabel("searchtext", "_", Color.cyan, 50, 65);
            if (this.SearchResults != null)
            {
                int count = this.SearchResults.Count;
                int spacing = 60; //Spacing between each registry line
                int yoffset = 100; //Offset below button row
                int labeloffset = 60; //x offset for label from icon

                for (int n = 0; n < count; n++)
                {
                    this.manager.AddIcon("itemicon" + n, "empty", Color.white, 0, yoffset + (spacing * n));
                    this.manager.AddBigLabel("iteminfo" + n, "Inventory Item", Color.white, labeloffset, yoffset + (spacing * n));
                }
            }
        }
        StockpileLiftControllerWindow.dirty = true;
        StockpileLiftControllerWindow.networkRedraw = false;
    }

    public override void UpdateMachine(SegmentEntity targetEntity)
    {
        StockpileLiftController controller = (targetEntity as StockpileLiftController);
        //Catch for when the window is called on an inappropriate machine
        if (controller == null)
        {
            GenericMachinePanelScript.instance.Hide();
            UIManager.RemoveUIRules("Machine");
            return;
        }
        //UIUtil.UIdelay = 0;

        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
        if (!this.registerstock && !this.ItemSearch)
        {
            this.manager.UpdatePowerBar("power", center.mrCurrentPower, center.mrMaxPower);
            this.errorString = string.Empty;
            if (center.mOperatingState != this.lastState)
                StockpileLiftControllerWindow.dirty = true;
            if (StockpileLiftControllerWindow.networkRedraw)
            {
                this.Redraw(targetEntity);
                StockpileLiftControllerWindow.dirty = true;
                StockpileLiftControllerWindow.networkRedraw = false;
            }
            if (targetEntity.mbNetworkUpdated)
            {
                StockpileLiftControllerWindow.dirty = true;
                targetEntity.mbNetworkUpdated = false;
            }
            if (StockpileLiftControllerWindow.dirty)
            {
                this.UpdateState(center);
                StockpileLiftControllerWindow.dirty = false;
            }
            if (center.mLiftMob != null)
                this.UpdateLift(center);
            else if (center.mSpawnData != null)
                this.UpdateClientLift(center);
            this.manager.UpdateLabel("error_label", this.errorString, Color.red);
        }
        else if (this.registerstock && !this.ItemSearch)
        {
            if (StockpileLiftControllerWindow.networkRedraw)
            {
                this.Redraw(targetEntity);
                StockpileLiftControllerWindow.dirty = true;
                StockpileLiftControllerWindow.networkRedraw = false;
            }
            if (StockpileLiftControllerWindow.dirty)
            {
                this.UpdateStock(center);
                StockpileLiftControllerWindow.dirty = false;
            }
        }
        else if (this.ItemSearch)
        {
            if (this.SearchResults == null)
            {
                this.Counter++;
                foreach (char c in Input.inputString)
                {
                    if (c == "\b"[0])  //Backspace
                    {
                        if (this.EntryString.Length != 0)
                            this.EntryString = this.EntryString.Substring(0, this.EntryString.Length - 1);
                    }
                    else if (c == "\n"[0] || c == "\r"[0]) //Enter or Return
                    {
                        this.SearchResults = new List<ItemBase>();

                        for (int n = 0; n < ItemEntry.mEntries.Length; n++)
                        {
                            if (ItemEntry.mEntries[n] == null) continue;
                            if (ItemEntry.mEntries[n].Name.ToLower().Contains(this.EntryString.ToLower()))
                                this.SearchResults.Add(ItemManager.SpawnItem(ItemEntry.mEntries[n].ItemID));
                        }
                        for (int n = 0; n < TerrainData.mEntries.Length; n++)
                        {
                            bool foundvalue = false;
                            if (TerrainData.mEntries[n] == null) continue;
                            if (TerrainData.mEntries[n].Name.ToLower().Contains(this.EntryString.ToLower()))
                            {
                                int count = TerrainData.mEntries[n].Values.Count;
                                for (int m = 0; m < count; m++)
                                {
                                    if (TerrainData.mEntries[n].Values[m].Name.ToLower().Contains(this.EntryString.ToLower()))
                                    {
                                        this.SearchResults.Add(ItemManager.SpawnCubeStack(TerrainData.mEntries[n].CubeType, TerrainData.mEntries[n].Values[m].Value, 1));
                                        foundvalue = true;
                                    }
                                }
                                if (!foundvalue)
                                    this.SearchResults.Add(ItemManager.SpawnCubeStack(TerrainData.mEntries[n].CubeType, TerrainData.mEntries[n].DefaultValue, 1));
                            }
                            if ((this.EntryString.ToLower().Contains("component") || this.EntryString.ToLower().Contains("placement") || this.EntryString.ToLower().Contains("multi")) && TerrainData.mEntries[n].CubeType == 600)
                            {
                                int count = TerrainData.mEntries[n].Values.Count;
                                for (int m = 0; m < count; m++)
                                {
                                    this.SearchResults.Add(ItemManager.SpawnCubeStack(600, TerrainData.mEntries[n].Values[m].Value, 1));
                                }
                            }
                        }
                        if (this.SearchResults.Count == 0)
                            this.SearchResults = null;

                        UIManager.mbEditingTextField = false;
                        UIManager.RemoveUIRules("TextEntry");

                        this.manager.RedrawWindow();
                        return;
                    }
                    else
                        this.EntryString += c;
                }
                this.manager.UpdateLabel("searchtext", this.EntryString + (this.Counter % 20 > 10 ? "_" : ""), Color.cyan);
                StockpileLiftControllerWindow.dirty = true;
                return;
            }
            else
            {
                this.manager.UpdateLabel("searchtitle", "Searching for:", Color.white);
                this.manager.UpdateLabel("searchtext", this.EntryString, Color.cyan);
                int count = this.SearchResults.Count;
                for (int n = 0; n < count; n++)
                {
                    ItemBase item = this.SearchResults[n];
                    string itemname = ItemManager.GetItemName(item);
                    string iconname = ItemManager.GetItemIcon(item);

                    this.manager.UpdateIcon("itemicon" + n, iconname, Color.white);
                    this.manager.UpdateLabel("iteminfo" + n, itemname, Color.white);
                }
            }
        }
    }

    private void UpdateLift(StockpileLiftController controller)
    {
        StockpileLiftMob cargoLiftMob = controller.mLiftMob;
        if (cargoLiftMob == null)
            return;
        Color color = Color.white;
        if (cargoLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Stuck)
        {
            if (controller.mOperatingState == StockpileLiftController.OperatingState.Checking || controller.mOperatingState == StockpileLiftController.OperatingState.InstallingRails)
            {
                color = Color.cyan;
                this.errorString = "Lift stuck. Please wait for rail scan to complete.";
            }
            else
            {
                color = Color.red;
                if (string.IsNullOrEmpty(this.errorString))
                    this.errorString = "Lift stuck below shaft depth. Check for blockages";
            }
        }
        this.manager.UpdateLabel("lift_state", "State: " + cargoLiftMob.mOperatingState, color);
        this.manager.UpdateLabel("lift_order", "Order: " + cargoLiftMob.mCurrentLoadOrder.ToString(), Color.white);
        this.manager.UpdateLabel("lift_depth", "Depth: " + controller.GetDepth(cargoLiftMob.mnY, cargoLiftMob.mRenderOffset).ToString("N0") + "m", Color.white);
        this.manager.UpdateLabel("lift_speed", "Speed: " + cargoLiftMob.mrSpeed.ToString("N0"), Color.white);
        this.manager.UpdateLabel("lift_cargo", string.Format("Cargo: {0:N0} / {1:N0}", cargoLiftMob.mnUsedStorage, cargoLiftMob.mnMaxStorage), Color.white);
    }

    private void UpdateStock(StockpileLiftController controller)
    {
        int count = 0;
        List<StockItem> stock = new List<StockItem>();
        if (controller.mLiftMob != null)
        {
            stock = controller.mLiftMob.StockLimits;
            count = stock.Count;
        }
        else
        {
            this.manager.UpdateLabel("stocktitle" + -1, "Add a Lift First", Color.red);
        }

        for (int index = 0; index < count; index++)
        {
            ItemBase item = stock[index].Item;
            int stocklimit = stock[index].StockLimit;

            string itemname = ItemManager.GetItemName(item);
            string iconname = ItemManager.GetItemIcon(item);

            this.manager.UpdateIcon("stockitem" + index, iconname, Color.white);
            this.manager.UpdateLabel("stocktitle" + index, itemname, Color.white);
            this.manager.UpdateLabel("stocklimit" + index, stocklimit.ToString("N0"), Color.white);
        }
    }

    private void UpdateClientLift(StockpileLiftController controller)
    {
        StockpileLiftMob.StockpileLiftData cargoLiftData = controller.mSpawnData;
        if (cargoLiftData == null || controller.mLiftMob == null)
            return;
        Color color = Color.white;
        if (cargoLiftData.operatingState == StockpileLiftMob.OperatingState.Stuck)
        {
            if (controller.mOperatingState == StockpileLiftController.OperatingState.Checking || controller.mOperatingState == StockpileLiftController.OperatingState.InstallingRails)
            {
                color = Color.cyan;
                this.errorString = "Lift stuck. Please wait for rail scan to complete.";
            }
            else
            {
                color = Color.red;
                if (string.IsNullOrEmpty(this.errorString))
                    this.errorString = "Lift stuck below shaft depth. Check for blockages";
            }
        }
        this.manager.UpdateLabel("lift_state", "State: " + cargoLiftData.operatingState, color);
        this.manager.UpdateLabel("lift_order", "Order: " + cargoLiftData.loadOrder.ToString(), Color.white);
        this.manager.UpdateLabel("lift_depth", "Depth: " + controller.GetDepth(cargoLiftData.y, cargoLiftData.renderOffset).ToString("N0") + "m", Color.white);
        this.manager.UpdateLabel("lift_speed", "Speed: " + cargoLiftData.speed.ToString("N0"), Color.white);
        this.manager.UpdateLabel("lift_cargo", string.Format("Cargo: {0:N0} / {1:N0}", cargoLiftData.usedSpace, StockpileLiftMob.GetMaxCargo(cargoLiftData.type)), Color.white);
    }

    private string GetReadableState(StockpileLiftController.RailState railState)
    {
        switch (railState)
        {
            case StockpileLiftController.RailState.WaitingForResources:
                return "Waiting for Rack Rail";
            case StockpileLiftController.RailState.Scanning:
            case StockpileLiftController.RailState.Building:
            case StockpileLiftController.RailState.Blocked:
                return railState.ToString();
            default:
                return "Unknown State";
        }
    }

    private void UpdateState(StockpileLiftController controller)
    {
        this.manager.UpdateLabel("rail_depth", "Shaft depth: " + controller.mnRailDepth.ToString("N0"), Color.white);
        string str = string.Empty;
        Color color = Color.white;
        string label1;
        if (controller.mOperatingState == StockpileLiftController.OperatingState.InstallingRails || controller.mOperatingState == StockpileLiftController.OperatingState.Checking)
        {
            label1 = this.GetReadableState(controller.mRailState);
        }
        else
        {
            label1 = "Operating Lift";
            color = Color.green;
        }
        if (controller.mOperatingState != StockpileLiftController.OperatingState.Operating && controller.mRailState == StockpileLiftController.RailState.WaitingForResources)
            this.errorString = "Controller has no Rack Rail to place";
        this.manager.UpdateLabel("rail_state", label1, color);
        string label2 = string.Empty;
        if (controller.mOperatingState == StockpileLiftController.OperatingState.Checking)
            label2 = "Check Depth: " + (object)controller.mnRailCheckDepth;
        this.manager.UpdateLabel("rail_check_depth", label2, Color.yellow);
        ItemBase itemBase = controller.mLiftItem;
        if (itemBase != null)
        {
            this.manager.UpdateIcon("icon_lift", ItemManager.GetItemIcon(itemBase), Color.white);
            this.manager.UpdateLabel("icon_lift_name", ItemManager.GetItemName(itemBase), Color.white);
        }
        else
        {
            this.manager.UpdateIcon("icon_lift", "empty", Color.white);
            this.manager.UpdateLabel("icon_lift_name", "No lift deployed", Color.yellow);
        }
        this.manager.UpdateLabel("order_top", "Top: " + controller.mUpperLoadOrder.ToString(), Color.white);
        this.manager.UpdateLabel("order_bottom", "Bottom: " + controller.mLowerLoadOrder.ToString(), Color.white);
    }

    public override bool ButtonClicked(string name, SegmentEntity targetEntity)
    {
        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
            switch (name)
            {
            case "order_top_toggle":
                center.ToggleLoadMode("upper");
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceToggleLoadMode, "upper", (ItemBase)null, (SegmentEntity)center, 0.0f);
                StockpileLiftControllerWindow.dirty = true;
                return true;
            case "order_bottom_toggle":
                center.ToggleLoadMode("lower");
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceToggleLoadMode, "lower", (ItemBase)null, (SegmentEntity)center, 0.0f);
                StockpileLiftControllerWindow.dirty = true;
                return true;
            case "order_switch":
                center.ToggleLoadOrder();
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceToggleLoadOrder, "lower", (ItemBase)null, (SegmentEntity)center, 0.0f);
                StockpileLiftControllerWindow.dirty = true;
                return true;
            case "rail_check":
                center.ResetRailCheck();
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceResetRailCheck, "lower", (ItemBase)null, (SegmentEntity)center, 0.0f);
                StockpileLiftControllerWindow.dirty = true;
                return true;
            case "registerstocklimits":
                this.registerstock = true;
                this.manager.RedrawWindow();
                return true;
            case "stockcancel":
                this.registerstock = false;
                this.manager.RedrawWindow();
                return true;
            case "searchcancel":
                this.ItemSearch = false;
                this.SearchResults = null;
                UIManager.mbEditingTextField = false;
                UIManager.RemoveUIRules("TextEntry");
                this.EntryString = "";
                GenericMachinePanelScript.instance.Scroll_Bar.GetComponent<UIScrollBar>().scrollValue = 0.0f;
                this.manager.RedrawWindow();
                return true;
            }

        if (name.Contains("stockitem"))
        {
            int slotNum = -1;
            int.TryParse(name.Replace("stockitem", ""), out slotNum); //Get slot name as number
            List<StockItem> stock = new List<StockItem>();
            if (center.mLiftMob != null)
               stock = center.mLiftMob.StockLimits;

            if (slotNum > -1) // valid slot
            {
                //clear stockitem
                ItemBase item = stock[slotNum].Item;
                center.mLiftMob.RemoveStockItem(item);
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceRegisterLiftStock, "remove", item, (SegmentEntity)center, 0.0f);
                this.manager.RedrawWindow();
            }

            return true;
        }
        if (name.Contains("increasestock"))
        {
            int slotNum = -1;
            int.TryParse(name.Replace("increasestock", ""), out slotNum); //Get slot name as number
            if (slotNum > -1) // valid slot
            {
                int amount = 100;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    amount = 10;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    amount = 1;
                center.mLiftMob.StockLimits[slotNum].StockLimit += amount;
                if (center.mLiftMob.StockLimits[slotNum].StockLimit > center.mLiftMob.mnMaxStorage)
                    center.mLiftMob.StockLimits[slotNum].StockLimit = center.mLiftMob.mnMaxStorage;
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSetLiftStock, center.mLiftMob.StockLimits[slotNum].StockLimit.ToString(), center.mLiftMob.StockLimits[slotNum].Item, (SegmentEntity)center, 0.0f);
                StockpileLiftControllerWindow.dirty = true;
            }
            return true;
        }
        if (name.Contains("decreasestock"))
        {
            int slotNum = -1;
            int.TryParse(name.Replace("decreasestock", ""), out slotNum); //Get slot name as number
            if (slotNum > -1) // valid slot
            {
                int amount = 100;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    amount = 10;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    amount = 1;
                center.mLiftMob.StockLimits[slotNum].StockLimit -= amount;
                if (center.mLiftMob.StockLimits[slotNum].StockLimit < 0)
                    center.mLiftMob.StockLimits[slotNum].StockLimit = 0;
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSetLiftStock, center.mLiftMob.StockLimits[slotNum].StockLimit.ToString(), center.mLiftMob.StockLimits[slotNum].Item, (SegmentEntity)center, 0.0f);
                StockpileLiftControllerWindow.dirty = true;
            }
            return true;
        }
        if (name.Contains("itemicon"))
        {
            int slotNum = -1;
            int.TryParse(name.Replace("itemicon", ""), out slotNum); //Get slot name as number
            if (slotNum > -1)
            {
                center.mLiftMob.AddStockItem(this.SearchResults[slotNum]);
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceRegisterLiftStock, "0", this.SearchResults[slotNum], (SegmentEntity)center, 0.0f);
                this.SearchResults = null;
                this.ItemSearch = false;
                this.EntryString = "";
                GenericMachinePanelScript.instance.Scroll_Bar.GetComponent<UIScrollBar>().scrollValue = 0.0f;
                this.manager.RedrawWindow();
            }
        }
        return false;
    }

    public override bool ButtonRightClicked(string name, SegmentEntity targetEntity)
    {
        if (name.Contains("stockitem"))
        {
            this.ItemSearch = true;
            UIManager.mbEditingTextField = true;
            UIManager.AddUIRules("TextEntry", UIRules.RestrictMovement | UIRules.RestrictLooking | UIRules.RestrictBuilding | UIRules.RestrictInteracting | UIRules.SetUIUpdateRate);
            this.Redraw(targetEntity);
            GenericMachinePanelScript.instance.Scroll_Bar.GetComponent<UIScrollBar>().scrollValue = 0.0f;
            return true;
        }
        else
            return base.ButtonRightClicked(name, targetEntity);
    }

    public override ItemBase GetDragItem(string name, SegmentEntity targetEntity)
    {
        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
        if (name.Equals("icon_lift"))
            return center.GetLift();
        return (ItemBase)null;
    }

    public override bool RemoveItem(string name, ItemBase originalItem, ItemBase swapitem, SegmentEntity targetEntity)
    {
        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
        if (name.Equals("icon_lift"))
        {
            if (swapitem == null)
            {
                center.SwapLift((ItemBase)null);
                StockpileLiftControllerWindow.dirty = true;
                this.Redraw(targetEntity);
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCargoLift, (string)null, (ItemBase)null, (SegmentEntity)center, 0.0f);
                return true;
            }
            if (center.IsValidLift(swapitem) && (swapitem as ItemStack).mnAmount <= 1)
            {
                center.SwapLift(swapitem);
                StockpileLiftControllerWindow.dirty = true;
                this.Redraw(targetEntity);
                if (!WorldScript.mbIsServer)
                    NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCargoLift, (string)null, swapitem, (SegmentEntity)center, 0.0f);
                return true;
            }
        }
        return false;
    }

    public override void HandleItemDrag(string name, ItemBase draggedItem, DragAndDropManager.DragRemoveItem dragDelegate, SegmentEntity targetEntity)
    {
        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
        if (name.Contains("stockitem"))
        {
            int slotNum = -2;
            int.TryParse(name.Replace("stockitem", ""), out slotNum); //Get slot name as number

            if (slotNum == -1) // valid slot
            {
                if (this.manager.mWindowLookup[name + "_icon"].GetComponent<UISprite>().spriteName == "empty")
                {
                    if (center.mLiftMob == null)
                        return;
                    center.mLiftMob.AddStockItem(draggedItem);
                    if (!WorldScript.mbIsServer)
                        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceRegisterLiftStock, "0", draggedItem, (SegmentEntity)center, 0.0f);
                    this.manager.RedrawWindow();
                }
            }
        }
        
        if (!name.Equals("icon_lift") || !center.IsValidLift(draggedItem) || !center.CanDeployLift())
            return;
        ItemStack itemStack = draggedItem as ItemStack;
        ItemBase lift = center.GetLift();
        if (itemStack.mnAmount > 1)
        {
            if (lift != null && WorldScript.mLocalPlayer.mInventory.FitItem(lift) == 0)
            {
                Debug.Log((object)"Trying to fit stack, but player inventory can't fit replacement motor");
                return;
            }
            --itemStack.mnAmount;
        }
        else if (!dragDelegate(draggedItem, lift))
            return;
        ItemBase itemBase = ItemManager.CloneItem(draggedItem);
        ItemManager.SetItemCount(itemBase, 1);
        center.SwapLift(itemBase);
        StockpileLiftControllerWindow.dirty = true;
        this.Redraw(targetEntity);
        InventoryPanelScript.MarkDirty();
        if (WorldScript.mbIsServer)
            return;
        NetworkManager.instance.SendInterfaceCommand(InterfaceName, InterfaceSwapCargoLift, (string)null, draggedItem, (SegmentEntity)center, 0.0f);
    }

    public override void OnClose(SegmentEntity targetEntity)
    {
        this.registerstock = false;
        this.ItemSearch = false;
        this.SearchResults = null;
        UIManager.mbEditingTextField = false;
        UIManager.RemoveUIRules("TextEntry");
        this.EntryString = "";
        GenericMachinePanelScript.instance.Scroll_Bar.GetComponent<UIScrollBar>().scrollValue = 0.0f;
        if ((targetEntity as StockpileLiftController) == null)
            return;
        base.OnClose((targetEntity as StockpileLiftController).GetCenter());
    }

    public static NetworkInterfaceResponse HandleNetworkCommand(Player player, NetworkInterfaceCommand nic)
    {
        StockpileLiftController StockpileLiftController = nic.target as StockpileLiftController;
        string key = nic.command;
        if (key != null)
        {
            switch (key)
            {
                case InterfaceToggleLoadMode:
                    StockpileLiftController.ToggleLoadMode(nic.payload);
                    StockpileLiftControllerWindow.dirty = true;
                    break;
                case InterfaceToggleLoadOrder:
                    StockpileLiftController.ToggleLoadOrder();
                    StockpileLiftControllerWindow.dirty = true;
                    break;
                case InterfaceSwapCargoLift:
                    StockpileLiftController.SwapLift(nic.itemContext);
                    StockpileLiftControllerWindow.networkRedraw = true;
                    break;
                case InterfaceResetRailCheck:
                    StockpileLiftController.ResetRailCheck();
                    StockpileLiftControllerWindow.dirty = true;
                    break;
                case InterfaceSetLiftStock:
                    int stocklimit;
                    int.TryParse(nic.payload ?? "-1", out stocklimit);
                    if (StockpileLiftController.mLiftMob != null)
                        StockpileLiftController.mLiftMob.SetNetworkStock(nic.itemContext, stocklimit);
                    StockpileLiftControllerWindow.dirty = true;
                    break;
                case InterfaceRegisterLiftStock:
                    bool removeitem = nic.payload == "remove";
                    if (StockpileLiftController.mLiftMob != null)
                        StockpileLiftController.mLiftMob.NewNetworkStock(nic.itemContext, removeitem);
                    StockpileLiftControllerWindow.networkRedraw = true;
                    break;
            }
        }
        return new NetworkInterfaceResponse()
        {
            entity = (SegmentEntity)StockpileLiftController,
            inventory = player.mInventory
        };
    }
    

    public override List<HandbookContextEntry> GetContextualHelp(SegmentEntity targetEntity)
    {
        List<HandbookContextEntry> list = new List<HandbookContextEntry>();
        StockpileLiftController center = (targetEntity as StockpileLiftController).GetCenter();
        list.Add(HandbookContextEntry.Material(750, ModManager.mModMappings.CubesByKey["steveman0.StockpileLiftController"].CubeType, center.mValue, "Selected Machine"));
        list.Add(HandbookContextEntry.Material(749, (ushort)602, (ushort)0, "Resource"));
        if (center.mLiftItem != null)
            list.Add(HandbookContextEntry.Material(748, center.mLiftItem.mnItemID, "Installed Lift"));
        return list;
    }
}
