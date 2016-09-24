using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using FortressCraft.Community.Utilities;


public class StockpileLiftMob : MobEntity
{
    public int mnMaxStorage = 1;
    public List<StorageMachineInterface> mAttachedHoppers = new List<StorageMachineInterface>();
    public List<MassStorageCrate> mAttachedMassStorage = new List<MassStorageCrate>();
    private float mrSpeedScalar = 1f;
    public float mrMaxSpeed = 1f;
    public float mrSpeed = 1f;
    public const float LOAD_TIMER = 1f;
    public const float SLOW_DOWN_DISTANCE = 10f;
    public const float APPROACH_SPEED = 0.5f;
    public const float ACCELERATION = 0.5f;
    private const int MB_X = 3;
    private const int MB_Y = 3;
    private const int MB_Z = 3;
    private const int MB_MIN_X = -1;
    private const int MB_MIN_Y = -1;
    private const int MB_MIN_Z = -1;
    private const int MB_MAX_X = 1;
    private const int MB_MAX_Y = 1;
    private const int MB_MAX_Z = 1;
    public StockpileLiftController mController;
    public int mnUsedStorage;
    public List<ItemBase> mCargo;
    public StockpileLiftMob.OperatingState mOperatingState;
    public StockpileLiftMob.Direction mDirection;
    public StockpileLiftMob.LoadingState mLoadingState;
    public StockpileLiftMob.CargoLiftType mType;
    public StockpileLiftMob.CargoLoadOrder mCurrentLoadOrder;
    public long mTargetHeight;
    public float mrLoadTimer;
    public float mRenderOffset;
    public float mPreviousTimeStep;
    private int mnHopperRoundRobinPosition;
    private int mnStorageCrateRoundRobinPosition;
    private bool mbOutOfPower;
    private int mnUpdates;
    private bool mbPreloadedNextSegment;
    private Segment mPrevGetSeg;
    public float mrOperatingStateTimer;
    public int mnCurrentSideIndex;
    public int mnCurrentSide;
    private int UnloadAllDelay = -1;

    public List<StockItem> StockLimits = new List<StockItem>();
    public bool StillUnloading = false;

    public List<KeyValuePair<ConveyorEntity, StorageMachineInterface>> mAttachedConveyors = new List<KeyValuePair<ConveyorEntity, StorageMachineInterface>>();

    public StockpileLiftMob(ModCreateMobParameters parameters)
    : base(MobType.Mod, parameters.MobNumber, SpawnableObjectEnum.CargoLift)
  {
        if (WorldScript.mbIsServer)
            this.mFrustrum = SegmentManagerThread.instance.RequestMachineFrustrum();
        this.mrSpeed = 0.17345f;
        this.SetLiftType(StockpileLiftMob.CargoLiftType.Basic);
        this.mnHealth = 1;
        this.mnUsedStorage = 0;
        this.mCargo = new List<ItemBase>();
        this.SetNewOperatingState(StockpileLiftMob.OperatingState.Unloading);
        this.mDirection = StockpileLiftMob.Direction.Up;
        this.mbHostile = false;
    }

    private void SetNewOperatingState(StockpileLiftMob.OperatingState lState)
    {
        this.mOperatingState = lState;
        this.mrOperatingStateTimer = 0.0f;
        this.mbNetworkUpdateRequested = true;
    }

    public override void OnUnload()
    {
    }

    public void SetLiftType(StockpileLiftMob.CargoLiftType type)
    {
        this.mType = type;
        switch (type)
        {
            case StockpileLiftMob.CargoLiftType.Basic:
                this.mnMaxStorage = 300;
                this.mrMaxSpeed = 5f;
                break;
            case StockpileLiftMob.CargoLiftType.Improved:
                this.mnMaxStorage = 1000;
                this.mrMaxSpeed = 10f;
                break;
            case StockpileLiftMob.CargoLiftType.Bulk:
                this.mnMaxStorage = 3000;
                this.mrMaxSpeed = 15f;
                break;
        }
    }

    public override void SpawnGameObject()
    {
        base.SpawnGameObject();
    }

    public override int TakeDamage(int lnDamage)
    {
        return -1;
    }

    public void Spawn(long deployY)
    {
        long newY = deployY >> 4 << 4;
        if (newY != this.mnY)
            this.MoveMob(this.mnX, newY, this.mnZ, WorldHelper.DefaultBlockOffset);
        this.mRenderOffset = (float)(deployY - newY);
    }

    public void SetController(StockpileLiftController controller)
    {
        if (controller == null)
            Debug.LogError("Error, null controller?");
        this.mController = controller;
        if (this.mFrustrum == null)
            return;
        this.mFrustrum.RequestMainSegment(controller.mnX, controller.mnY, controller.mnZ);
    }

    public override void MobUpdate()
    {
        ++this.mnUpdates;
        this.UpdatePlayerDistanceInfo();
        this.UpdateOperatingState();
    }

    public void LowFrequencyUpdate()
    {
        if (WorldScript.mbIsServer && !this.mbActive)
            Debug.LogError("Error, StockpileLiftMob is not currently active, but is having LowFrequencyUpdates?");
        this.mrOperatingStateTimer += LowFrequencyThread.mrPreviousUpdateTimeStep;
        this.UpdateOperatingStateLF();
    }

    private void UpdateOperatingState()
    {
        switch (this.mOperatingState)
        {
            case StockpileLiftMob.OperatingState.Travelling:
                this.UpdateTravelling();
                break;
            case StockpileLiftMob.OperatingState.Stuck:
                this.UpdateStuck();
                break;
        }
    }

    private void SanityCheck()
    {
        this.Move(0.0f);
        if (this.mController == null)
            return;
        long num = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset + 0.5f);
        if (this.mOperatingState == StockpileLiftMob.OperatingState.Loading)
        {
            if (this.mController.GetBottomOrder() == StockpileLiftMob.CargoLoadOrder.LoadAll || this.mController.GetBottomOrder() == StockpileLiftMob.CargoLoadOrder.LoadAny)
            {
                if (this.mTargetHeight == this.mController.GetBottomCoord())
                {
                    if (this.mTargetHeight != num)
                    {
                        Debug.LogError(("Error, we're unloading, but we're " + (this.mTargetHeight - num) + " m from loading height!"));
                        this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
                    }
                }
                else
                {
                    Debug.LogError("Error, Loading and Botton Order was Load, but target height was not at bottom!");
                    this.mTargetHeight = this.mController.GetBottomCoord();
                }
            }
            if (this.mController.GetTopOrder() == StockpileLiftMob.CargoLoadOrder.LoadAll || this.mController.GetTopOrder() == StockpileLiftMob.CargoLoadOrder.LoadAny)
            {
                if (this.mTargetHeight == this.mController.GetTopCoord())
                {
                    if (this.mTargetHeight != num)
                    {
                        Debug.LogError(("Error, we're unloading, but we're " + (this.mTargetHeight - num) + " m from loading height!"));
                        this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
                    }
                }
                else
                {
                    Debug.LogError("Error, Loading and Top Order was Load, but target height was not at Top!");
                    this.mTargetHeight = this.mController.GetTopCoord();
                }
            }
        }
        if (this.mOperatingState != StockpileLiftMob.OperatingState.Unloading)
            return;
        if (this.mController.GetBottomOrder() == StockpileLiftMob.CargoLoadOrder.UnloadAll || this.mController.GetBottomOrder() == StockpileLiftMob.CargoLoadOrder.UnloadAny)
        {
            if (this.mTargetHeight == this.mController.GetBottomCoord())
            {
                if (this.mTargetHeight != num)
                {
                    Debug.LogError(("Error, we're unloading, but we're " + (this.mTargetHeight - num) + " m from loading height!"));
                    this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
                }
            }
            else
            {
                Debug.LogError("Error, Unloading and Botton Order was UnLoad, but target height was not at bottom!");
                this.mTargetHeight = this.mController.GetBottomCoord();
            }
        }
        if (this.mController.GetTopOrder() != StockpileLiftMob.CargoLoadOrder.UnloadAll && this.mController.GetTopOrder() != StockpileLiftMob.CargoLoadOrder.UnloadAny)
            return;
        if (this.mTargetHeight == this.mController.GetTopCoord())
        {
            if (this.mTargetHeight == num)
                return;
            Debug.LogError(("Error, we're unloading, but we're " + (this.mTargetHeight - num) + " m from loading height!"));
            this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
        }
        else
        {
            Debug.LogError("Error, Unloading and Top Order was UnLoad, but target height was not at Top!");
            this.mTargetHeight = this.mController.GetTopCoord();
        }
    }

    private void UpdateOperatingStateLF()
    {
        if ((double)this.mrLoadTimer > 0.0)
        {
            this.mrLoadTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
        }
        else
        {
            switch (this.mOperatingState)
            {
                case StockpileLiftMob.OperatingState.Loading:
                    this.UpdateLoading();
                    this.SanityCheck();
                    break;
                case StockpileLiftMob.OperatingState.Unloading:
                    this.UpdateUnloading();
                    this.SanityCheck();
                    break;
            }
        }
    }

    private void UpdateLoading()
    {
        switch (this.mLoadingState)
        {
            case StockpileLiftMob.LoadingState.AcquiringInventories:
                this.UpdateAcquiringInventories(eHopperPermissions.RemoveOnly);
                break;
            case StockpileLiftMob.LoadingState.MovingItems:
                this.UpdateActuallyLoading();
                break;
            case StockpileLiftMob.LoadingState.CleanUp:
                this.UpdateCleanUp();
                break;
        }
    }

    private void UpdateAcquiringInventories(eHopperPermissions permission)
    {
        for (int index = 0; index < 16 && this.mLoadingState != StockpileLiftMob.LoadingState.MovingItems; ++index)
        {
            if (this.LookForMachines(permission))
            {
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.MovingItems);
                break;
            }
        }
    }

    private void UpdateCleanUp()
    {
        if ((double)this.mrOperatingStateTimer < 5.0)
            return;
        this.mAttachedHoppers.Clear();
        this.mAttachedMassStorage.Clear();
        if (!WorldScript.mbIsServer)
            return;
        this.mDirection = this.mDirection != StockpileLiftMob.Direction.Up ? StockpileLiftMob.Direction.Up : StockpileLiftMob.Direction.Down;
        if (this.mController.mOperatingState != StockpileLiftController.OperatingState.Operating)
        {
            //ServerConsole.DebugLog("Lift stuck because Controler Operating state was " + this.mController.mOperatingState, ConsoleMessageType.Trace);
            this.SetNewOperatingState(StockpileLiftMob.OperatingState.Stuck);
        }
        else
        {
            if (this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.LoadAll && this.mnUsedStorage != this.mnMaxStorage)
            {
                Debug.LogError(("Error, we're Cleaning up and in LoadAll state, but we aren't full " + this.mnUsedStorage));
                this.CheckStorage();
            }
            if (this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.UnloadAll && this.mnUsedStorage != 0 && this.StillUnloading)
            {
                foreach (ItemBase item in this.mCargo)
                {
                    if (item != null && !this.StockLimits.Exists(x => x.Item.Compare(item)))
                    {
                        Debug.LogError(("Error, we're Cleaning up and in UnLoadAll state, but we are empty! " + this.mnUsedStorage));
                        this.CheckStorage();
                        break;
                    }
                }
            }
            if (this.mDirection == StockpileLiftMob.Direction.Up)
            {
                this.mCurrentLoadOrder = this.mController.GetTopOrder();
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
                this.mTargetHeight = this.mController.GetTopCoord();
            }
            else
            {
                this.mCurrentLoadOrder = this.mController.GetBottomOrder();
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
                this.mTargetHeight = this.mController.GetBottomCoord();
            }
            this.mbNetworkUpdateRequested = true;
        }
    }

    private void UpdateActuallyLoading()
    {
        if (this.mnUsedStorage >= this.mnMaxStorage)
        {
            this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
        }
        else
        {
            int num1 = 50;
            if (this.mType == StockpileLiftMob.CargoLiftType.Basic)
                num1 = 50;
            if (this.mType == StockpileLiftMob.CargoLiftType.Improved)
                num1 = 250;
            if (this.mType == StockpileLiftMob.CargoLiftType.Bulk)
                num1 = 500;
            int num2 = 0;
            int num3 = 0;
            for (int index = 0; index < this.mAttachedHoppers.Count; ++index)
            {
                StorageMachineInterface machineInterface = this.mAttachedHoppers[index];
                num2 += machineInterface.UsedCapacity;
            }
            float num4 = 1f;
            if (num2 > this.mnMaxStorage)
                num4 = (float) this.mnMaxStorage / (float) num2;
            for (int index = 0; index < this.mAttachedHoppers.Count && num3 <= num1; ++index)
            {
                StorageMachineInterface machineInterface = this.mAttachedHoppers[index];
                int amountToExtract = (int)((double)machineInterface.UsedCapacity * (double)num4);
                if (amountToExtract > machineInterface.UsedCapacity)
                    amountToExtract = machineInterface.UsedCapacity;
                if (amountToExtract > num1)
                    amountToExtract = num1;
                if (amountToExtract > this.mnMaxStorage - this.mnUsedStorage)
                    amountToExtract = this.mnMaxStorage - this.mnUsedStorage;
                if (amountToExtract != 0)
                {
                    //int num5 = machineInterface.UnloadToList(this.mCargo, amountToExtract);
                    //Injected my smart loading function that accounts for stock limits
                    int num5 = this.SmartLoad(machineInterface, amountToExtract);
                    this.mnUsedStorage += num5;
                    num3 += num5;
                    if (this.mnUsedStorage >= this.mnMaxStorage)
                        break;
                }
            }
            if (num3 > 0 && this.mnUsedStorage < this.mnMaxStorage && FloatingCombatTextManager.instance != null)
            {
                long lnY = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
                FloatingCombatTextManager.instance.QueueText(this.mnX, lnY + 1L, this.mnZ, 1f, "Loaded " + num3.ToString() + " items!", Color.green, 2f, 64f);
                FloatingCombatTextManager.instance.QueueText(this.mnX, lnY, this.mnZ, 1f, (this.mnMaxStorage - this.mnUsedStorage) + " capacity free!", Color.green, 2f, 64f);
            }
            if (this.mnUsedStorage >= this.mnMaxStorage)
            {
                if ((Object)FloatingCombatTextManager.instance != (Object)null)
                {
                    long num5 = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
                    FloatingCombatTextManager.instance.QueueText(this.mnX, num5 + 2L, this.mnZ, 1f, "Loading Complete!", Color.green, 2f, 64f);
                }
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
            }
            else
            {
                if ((double)this.mrOperatingStateTimer <= 5.0)
                    return;
                if (this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.LoadAll)
                {
                    this.mrLoadTimer = 1f;
                    this.mrOperatingStateTimer = 0.0f;
                    this.SetNewLoadingState(StockpileLiftMob.LoadingState.AcquiringInventories);
                }
                else
                    this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
            }
        }
    }

    private int SmartLoad(StorageMachineInterface hopper, int amounttoextract)
    {
        int amount = amounttoextract;
        ItemBase itemToFit1;
        hopper.TryExtractAny(null, amounttoextract, out itemToFit1);
        if (itemToFit1 != null)
        {
            int currentStackSize = ItemManager.GetCurrentStackSize(itemToFit1);
            if (currentStackSize > 0)
            {
                StockItem stockitem = this.StockLimits.Where(x => x.Item.Compare(itemToFit1)).FirstOrDefault();
                if (stockitem != null)
                {
                    int freespace = stockitem.StockLimit - this.GetCurrentStock(itemToFit1);
                    //Only extract up to the stock limit, extras must be returned to the hopper
                    if (freespace <= 0)
                    {
                        hopper.TryInsert(this, itemToFit1);
                        return 0;
                    }
                    if (freespace >= amount)
                    {
                        if (currentStackSize > amount)
                        {
                            ItemManager.SetItemCount(itemToFit1, currentStackSize - amount);
                            ItemBase itemToFit2 = ItemManager.CloneItem(itemToFit1);
                            ItemManager.SetItemCount(itemToFit2, amount);
                            ItemManager.FitCargo(this.mCargo, itemToFit2);
                            hopper.TryInsert(null, itemToFit1);
                            return amounttoextract;
                        }
                        amount -= currentStackSize;
                        ItemManager.FitCargo(this.mCargo, itemToFit1);
                    }
                    else
                    {
                        if (currentStackSize > freespace)
                        {
                            ItemManager.SetItemCount(itemToFit1, currentStackSize - freespace);
                            ItemBase itemToFit2 = ItemManager.CloneItem(itemToFit1);
                            ItemManager.SetItemCount(itemToFit2, freespace);
                            ItemManager.FitCargo(this.mCargo, itemToFit2);
                            hopper.TryInsert(null, itemToFit1);
                            return freespace;
                        }
                        amount -= currentStackSize;
                        ItemManager.FitCargo(this.mCargo, itemToFit1);
                    }
                }
                else
                {
                    if (currentStackSize > amount)
                    {
                        ItemManager.SetItemCount(itemToFit1, currentStackSize - amount);
                        ItemBase itemToFit2 = ItemManager.CloneItem(itemToFit1);
                        ItemManager.SetItemCount(itemToFit2, amount);
                        ItemManager.FitCargo(this.mCargo, itemToFit2);
                        hopper.TryInsert(null, itemToFit1);
                        return amounttoextract;
                    }
                    amount -= currentStackSize;
                    ItemManager.FitCargo(this.mCargo, itemToFit1);
                }
            }
        }
        return amounttoextract - amount;
    }

    private int GetCurrentStock(ItemBase item)
    {
        if (item.mType == ItemType.ItemCubeStack)
        {
            ItemBase cargoitem = this.mCargo.Where(x => x.Compare(item)).FirstOrDefault();
            if (cargoitem != null)
                return (cargoitem as ItemCubeStack).mnAmount;
            else
                return 0;
        }
        else if (item.mType == ItemType.ItemStack)
        {
            ItemBase cargoitem = this.mCargo.Where(x => x.Compare(item)).FirstOrDefault();
            if (cargoitem != null)
                return (cargoitem as ItemStack).mnAmount;
            else
                return 0;
        }
        else
            return this.mCargo.Where(x => x.Compare(item)).Count();
    }

    private void UpdateUnloading()
    {
        switch (this.mLoadingState)
        {
            case StockpileLiftMob.LoadingState.AcquiringInventories:
                if (this.mCargo.Count == 0)
                {
                    long lnY = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
                    if (FloatingCombatTextManager.instance != null)
                        FloatingCombatTextManager.instance.QueueText(this.mnX, lnY + 1L, this.mnZ, 1f, "Lift is empty!", Color.red, 2f, 64f);
                    this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
                    break;
                }
                this.UpdateAcquiringInventories(eHopperPermissions.AddOnly);
                break;
            case StockpileLiftMob.LoadingState.MovingItems:
                this.UpdateActuallyUnloading();
                break;
            case StockpileLiftMob.LoadingState.CleanUp:
                this.UpdateCleanUp();
                break;
        }
    }

    private void SetNewLoadingState(StockpileLiftMob.LoadingState lNewState)
    {
        this.mLoadingState = lNewState;
        if (this.mLoadingState == StockpileLiftMob.LoadingState.CleanUp)
        {
            if (this.mController != null)
                this.mController.MarkDirtyDelayed();
            this.mnCurrentSideIndex = 0;
            this.mnCurrentSide = 0;
        }
        if (this.mLoadingState == StockpileLiftMob.LoadingState.AcquiringInventories)
        {
            this.mnCurrentSideIndex = 0;
            this.mnCurrentSide = 0;
        }
        long lnY = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
        if ((Object)FloatingCombatTextManager.instance != (Object)null)
            FloatingCombatTextManager.instance.QueueText(this.mnX, lnY + 1L, this.mnZ, 0.75f, this.mLoadingState.ToString(), Color.cyan, 2f, 16f);
        this.mbNetworkUpdateRequested = true;
    }

    private ItemBase GetNextCargo(ref int startIndex)
    {
        for (int index = startIndex; index < this.mCargo.Count; ++index)
        {
            if (this.mCargo[index] != null)
            {
                startIndex = index;
                return this.mCargo[index];
            }
        }
        return (ItemBase)null;
    }

    private void UpdateActuallyUnloading()
    {
        if (this.mCargo.Count == 0)
        {
            long lnY = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
            FloatingCombatTextManager.instance.QueueText(this.mnX, lnY + 1L, this.mnZ, 1f, "Unloading finished - Lift has no Cargo!", Color.red, 2f, 64f);
            this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
        }
        else
        {
            int startIndex = 0;
            ItemBase nextCargo = this.GetNextCargo(ref startIndex);
            if (nextCargo == null)
            {
                long lnY = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
                FloatingCombatTextManager.instance.QueueText(this.mnX, lnY + 1L, this.mnZ, 1f, "Error, failed to locate a non-null item to unload!", Color.red, 2f, 64f);
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
            }
            else
            {
                //initializing variable before main loop
                int num1 = 0;
                int currentStackSize = ItemManager.GetCurrentStackSize(nextCargo);
                int hopperindex = 0;

                //Rough idea of how many times we'd need to run
                for (int n = 0; n < this.mCargo.Count; n++)
                {
                    //Check for which of the inventories we need to check over
                    if (this.StockLimits.Exists(x => x.Item.Compare(nextCargo)))
                    {
                        //Look at all filtered hoppers to place the restricted item
                        for (int m = 0; m < this.mAttachedConveyors.Count; m++)
                        {
                            KeyValuePair<ConveyorEntity, StorageMachineInterface> conveyorpair = this.mAttachedConveyors[m];
                            if (this.ExemplarCompare(nextCargo, conveyorpair.Key) && conveyorpair.Value.IsNotFull())
                            {
                                if (conveyorpair.Value != null && !((SegmentEntity)conveyorpair.Value).mbDelete && !conveyorpair.Value.IsFull())
                                {
                                    int num2 = conveyorpair.Value.TryPartialInsert(null, ref nextCargo, false, true);
                                    currentStackSize -= num2;
                                    this.mnUsedStorage -= num2;
                                    num1 += num2;
                                }
                                //Finished so quit searching
                                if (currentStackSize == 0)
                                {
                                    this.mCargo[startIndex] = (ItemBase)null;
                                    break;
                                }
                            }
                        }
                        //Went through the entire list or completed the current stack
                        startIndex++;
                        nextCargo = this.GetNextCargo(ref startIndex);
                        if (nextCargo != null)
                            currentStackSize = ItemManager.GetCurrentStackSize(nextCargo);
                        else
                            break;
                    }
                    else //Not restricted item -> dump in any hopper
                    {
                        //Check if all hoppers are full
                        if (hopperindex >= this.mAttachedHoppers.Count)
                        {
                            //Get next cargo in hope of finding a filtered one
                            //this.StillUnloading = true;
                            nextCargo = this.GetNextCargo(ref startIndex);
                            if (nextCargo != null)
                                currentStackSize = ItemManager.GetCurrentStackSize(nextCargo);
                            else
                                break;
                        }
                        else 
                        {
                            StorageMachineInterface machineInterface = this.mAttachedHoppers[hopperindex];
                            if (machineInterface != null && !((SegmentEntity)machineInterface).mbDelete && !machineInterface.IsFull())
                            {
                                int num2 = machineInterface.TryPartialInsert(null, ref nextCargo, false, true);
                                currentStackSize -= num2;
                                this.mnUsedStorage -= num2;
                                num1 += num2;
                            }
                            else
                                hopperindex++;
                            if (currentStackSize == 0)
                            {
                                //this.StillUnloading = this.CheckUnloadAllStay();
                                this.mCargo[startIndex] = (ItemBase)null;
                                nextCargo = this.GetNextCargo(ref startIndex);
                                if (nextCargo != null)
                                    currentStackSize = ItemManager.GetCurrentStackSize(nextCargo);
                                else
                                    break;
                            }
                            else
                                n--;
                        }
                    }
                }

                if (num1 > 0)
                {
                    long lnY = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
                    if ((Object)FloatingCombatTextManager.instance != null)
                    {
                        FloatingCombatTextManager.instance.QueueText(this.mnX, lnY + 1L, this.mnZ, 1f, "Unloaded " + num1 + " items!", Color.green, 2f, 64f);
                        FloatingCombatTextManager.instance.QueueText(this.mnX, lnY, this.mnZ, 1f, (this.mnMaxStorage - this.mnUsedStorage) + " capacity free!", Color.green, 2f, 64f);
                    }
                }
                if (this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.UnloadAll && this.CheckUnloadAllStay())
                {
                    this.mrLoadTimer = 1f;
                    this.SetNewLoadingState(StockpileLiftMob.LoadingState.AcquiringInventories);
                    this.mnCurrentSideIndex = 0;
                    this.mnCurrentSide = 0;
                }
                else
                {
                    this.SetNewLoadingState(StockpileLiftMob.LoadingState.CleanUp);
                }
            }
        }
    }

    /// <summary>
    ///     Checks if there's at least one of each stocked item otherwise the lift can leave
    /// </summary>
    /// <returns>True if it still has each stocked type to unload</returns>
    private bool CheckUnloadAllStay()
    {
        //for (int m = 0; m<this.mCargo.Count; m++)
        //{
        //    if (this.mCargo[m] == null)
        //    {
        //        this.mCargo.RemoveAt(m);
        //        m--;
        //    }
        //}
        if (this.UnloadAllDelay > 0f)
        {
            this.UnloadAllDelay--;
            if (this.UnloadAllDelay <= 0)
                return false;
            return true;
        }
        bool matchfound = false;
        for (int n = 0; n < this.StockLimits.Count; n++)
        {
            //if (!this.mCargo.Exists(x => x.Compare(this.StockLimits[n].Item) && x.GetAmount() > 0))
            for (int m = 0; m < this.mCargo.Count; m++)
            {
                if (this.mCargo[m] != null && this.mCargo[m].Compare(this.StockLimits[n].Item) && this.mCargo[m].GetAmount() > 0)
                    matchfound = true;
            }
            if (!matchfound)
            {
                if (this.UnloadAllDelay < 0)
                    this.UnloadAllDelay = 3;
                return true;
            }
            else
                matchfound = false;
        }
        if (this.StockLimits.Count == 0 && this.mnUsedStorage > 0)
            return true;
        else if (this.mnUsedStorage == 0)
            return false;
        return true;
    }

    /// <summary>
    ///     Checks if an item matches an advanced conveyor filter's exemplar
    /// </summary>
    /// <param name="item">The item to check</param>
    /// <param name="conveyor">The (assumed) advanced conveyor filter</param>
    /// <returns>True for match</returns>
    private bool ExemplarCompare(ItemBase item, ConveyorEntity conveyor)
    {
        if (item.mnItemID > 0)
            return item.mnItemID == conveyor.ExemplarItemID;
        else if (item.mType == ItemType.ItemCubeStack)
            return ((item as ItemCubeStack).mCubeType == conveyor.ExemplarBlockID && (item as ItemCubeStack).mCubeValue == conveyor.ExemplarBlockValue);
        return false;
    }

    private void UpdateTravelling()
    {
        if (this.mController != null)
        {
            bool flag = this.mController.mPowerState == StockpileLiftController.PowerState.OutOfPower;
            if (this.mbOutOfPower != flag)
            {
                this.mbOutOfPower = flag;
                this.mbNetworkUpdateRequested = true;
            }
        }
        if (this.mbOutOfPower)
            this.mrSpeed *= 0.5f;
        float f = (float)(this.mTargetHeight - this.mnY) - this.mRenderOffset + (0.5f - this.mBlockOffset.y);
        if ((double)Mathf.Abs(f) < 0.00999999977648258)
        {
            if (this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.LoadAll || this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.LoadAny)
            {
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Loading);
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.AcquiringInventories);
                this.mnCurrentSideIndex = 0;
                this.mnCurrentSide = 0;
                this.mrSpeed = 0.0f;
            }
            else
            {
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Unloading);
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.AcquiringInventories);
                this.mnCurrentSideIndex = 0;
                this.mnCurrentSide = 0;
                this.mrSpeed = 0.0f;
            }
        }
        else
        {
            if ((double)f > 0.0)
            {
                if (!this.mbPreloadedNextSegment)
                    this.PreloadNextSegment(this.mnY + 16L);
                if ((double)f < 10.0)
                {
                    if ((double)this.mrSpeed > 0.5)
                    {
                        this.mrSpeed *= 0.9f;
                        this.mbNetworkUpdateRequested = true;
                    }
                    else
                        this.mrSpeed = 0.5f;
                }
                else
                {
                    this.mrSpeed += 0.5f * MobUpdateThread.mrPreviousUpdateTimeStep;
                    if ((double)this.mrSpeed > (double)this.mrMaxSpeed)
                        this.mrSpeed = this.mrMaxSpeed;
                }
            }
            if ((double)f < 0.0)
            {
                if (!this.mbPreloadedNextSegment)
                    this.PreloadNextSegment(this.mnY - 16L);
                if ((double)f > -10.0)
                {
                    if ((double)this.mrSpeed < -0.5)
                    {
                        this.mbNetworkUpdateRequested = true;
                        this.mrSpeed *= 0.9f;
                    }
                    else
                        this.mrSpeed = -0.5f;
                }
                else
                {
                    this.mrSpeed -= 0.5f * MobUpdateThread.mrPreviousUpdateTimeStep;
                    if ((double)this.mrSpeed < -(double)this.mrMaxSpeed)
                        this.mrSpeed = -this.mrMaxSpeed;
                }
            }
            float delta = this.mrSpeed * MobUpdateThread.mrPreviousUpdateTimeStep;
            if ((double)f > 0.0 && (double)delta > 0.0 && (double)delta > (double)f)
                delta = f;
            if ((double)f < 0.0 && (double)delta < 0.0 && (double)delta < (double)f)
                delta = f;
            this.Move(delta);
        }
    }

    private void PreloadNextSegment(long targetCoord)
    {
        if (this.AttemptGetSegment(this.mnX, targetCoord, this.mnZ) == null)
            return;
        this.mbPreloadedNextSegment = true;
    }

    private void Move(float delta)
    {
        float num = this.mRenderOffset + delta;
        if ((double)num >= 16.0)
        {
            if (!this.MoveMob(this.mnX, this.mnY + 16L, this.mnZ, this.mBlockOffset))
                return;
            num -= 16f;
            this.MarkDirtyDelayed();
        }
        if ((double)num < 0.0)
        {
            if (!this.MoveMob(this.mnX, this.mnY - 16L, this.mnZ, this.mBlockOffset))
                return;
            num += 16f;
            this.MarkDirtyDelayed();
        }
        this.mRenderOffset = num;
        this.mPreviousTimeStep = MobUpdateThread.mrPreviousUpdateTimeStep;
    }

    public bool MoveMob(long newX, long newY, long newZ, Vector3 newOffset)
    {
        long segX;
        long segY;
        long segZ;
        WorldHelper.GetSegmentCoords(newX, newY, newZ, out segX, out segY, out segZ);
        if (this.mSegment.baseX != segX || this.mSegment.baseY != segY || this.mSegment.baseZ != segZ)
        {
            Segment segment = this.AttemptGetSegment(newX, newY, newZ);
            if (segment == null)
                return false;
            if (segment == this.mSegment)
            {
                this.mnX = newX;
                this.mnY = newY;
                this.mnZ = newZ;
                this.mBlockOffset = newOffset;
                return true;
            }
            this.mSegment.RemoveMob((MobEntity)this, false);
            if (this.mFrustrum == null || this.mSegment == this.mFrustrum.mMainSegment)
                ;
            this.mSegment = segment;
            segment.AddMob((MobEntity)this, false);
            this.mbMovedToNewSegment = true;
            this.mbPreloadedNextSegment = false;
            if (segment.mbInLocalFrustrum)
            {
                if (this.mWrapper == null)
                    this.SpawnGameObject();
            }
            else if (this.mWrapper != null)
                this.DropGameObject();
        }
        this.mnX = newX;
        this.mnY = newY;
        this.mnZ = newZ;
        this.mBlockOffset = newOffset;
        return true;
    }

    private void UpdateStuck()
    {
    }

    public void SetStuck()
    {
        this.SetNewOperatingState(StockpileLiftMob.OperatingState.Stuck);
    }

    public void ResumeOrder()
    {
        if ((double)((float)(this.mTargetHeight - this.mnY) - this.mRenderOffset + (0.5f - this.mBlockOffset.y)) < 0.01)
        {
            if (this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.LoadAll || this.mCurrentLoadOrder == StockpileLiftMob.CargoLoadOrder.LoadAny)
            {
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Loading);
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.AcquiringInventories);
                this.mnCurrentSideIndex = 0;
                this.mnCurrentSide = 0;
                this.mrSpeed = 0.0f;
            }
            else
            {
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Unloading);
                this.SetNewLoadingState(StockpileLiftMob.LoadingState.AcquiringInventories);
                this.mnCurrentSideIndex = 0;
                this.mnCurrentSide = 0;
                this.mrSpeed = 0.0f;
            }
        }
        else
        {
            this.mrSpeed = 0.0f;
            this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
        }
    }

    private void RoundRobinSide(out int y, out int x, out int z)
    {
        if (this.mnCurrentSide == 0)
        {
            y = this.mnCurrentSideIndex / 3 - 1;
            x = -2;
            z = this.mnCurrentSideIndex % 3 - 1;
        }
        else if (this.mnCurrentSide == 1)
        {
            y = this.mnCurrentSideIndex / 3 - 1;
            x = 2;
            z = this.mnCurrentSideIndex % 3 - 1;
        }
        else if (this.mnCurrentSide == 2)
        {
            y = this.mnCurrentSideIndex / 3 - 1;
            x = this.mnCurrentSideIndex % 3 - 1;
            z = -2;
        }
        else if (this.mnCurrentSide == 3)
        {
            y = this.mnCurrentSideIndex / 3 - 1;
            x = this.mnCurrentSideIndex % 3 - 1;
            z = 2;
        }
        else
            x = y = z = 0;
    }

    private bool NextSide()
    {
        ++this.mnCurrentSideIndex;
        if (this.mnCurrentSideIndex == 9)
        {
            this.mnCurrentSideIndex = 0;
            ++this.mnCurrentSide;
        }
        return this.mnCurrentSide >= 4;
    }

    private bool LookForMachines(eHopperPermissions permission)
    {
        int y1;
        int x1;
        int z1;
        this.RoundRobinSide(out y1, out x1, out z1);
        long x2 = (long)x1 + this.mnX;
        long y2 = (long)y1 + this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset);
        long z2 = (long)z1 + this.mnZ;
        Segment segment = this.AttemptGetSegment(x2, y2, z2);
        if (segment == null)
            return false;
        SegmentEntity segmentEntity = segment.SearchEntity(x2, y2, z2);
        if (segment.GetCube(x2, y2, z2) != 1)
        {
            if (segmentEntity is StorageMachineInterface)
                this.AddAttachedHopper((StorageMachineInterface)segmentEntity, permission);
            if (segmentEntity is MassStorageCrate)
                this.AddAttachedMassStorage((MassStorageCrate)segmentEntity);
            //Add advanced conveyor filters
            if (segmentEntity is ConveyorEntity)
                this.AddAttachedConveyor((ConveyorEntity)segmentEntity);
        }
        return this.NextSide();
    }

    private void AddAttachedConveyor(ConveyorEntity conveyor)
    {
        if (conveyor.mValue != 12)
            return;
        for (int index = 0; index < this.mAttachedConveyors.Count; ++index)
        {
            ConveyorEntity conveyorentity = this.mAttachedConveyors[index].Key;
            if (conveyorentity != null)
            {
                if (conveyorentity.mbDelete || (this.mAttachedConveyors[index].Value != null && (this.mAttachedConveyors[index].Value as SegmentEntity).mbDelete))
                {
                    this.mAttachedConveyors.RemoveAt(index);
                    --index;
                }
                else if (conveyorentity == conveyor)
                    conveyor = null;
            }
        }
        if (conveyor == null)
            return;
        StorageMachineInterface hopper = this.GetConnectedHopper(conveyor);
        if (hopper != null)
            this.mAttachedConveyors.Add(new KeyValuePair<ConveyorEntity, StorageMachineInterface>(conveyor, hopper));
    }

    private StorageMachineInterface GetConnectedHopper(ConveyorEntity conv)
    { 
        long x = conv.mnX + (long)conv.mForwards.x;
        long y = conv.mnY + (long)conv.mForwards.y;
        long z = conv.mnZ + (long)conv.mForwards.z;

        Segment segment = this.AttemptGetSegment(x, y, z);
        if (segment == null)
            return null;
        SegmentEntity segmentEntity = segment.SearchEntity(x, y, z);
        segment.GetCube(x, y, z);
        if (segmentEntity is StorageMachineInterface)
            return segmentEntity as StorageMachineInterface;
        return null;
    }

    private void AddAttachedHopper(StorageMachineInterface hopper, eHopperPermissions permission)
    {
        for (int index = 0; index < this.mAttachedHoppers.Count; ++index)
        {
            StorageMachineInterface machineInterface = this.mAttachedHoppers[index];
            if (machineInterface != null)
            {
                if ((machineInterface as SegmentEntity).mbDelete)
                {
                    this.mAttachedHoppers.RemoveAt(index);
                    --index;
                }
                else
                {
                    if (machineInterface == hopper)
                    {
                        hopper = (StorageMachineInterface)null;
                        eHopperPermissions permissions = machineInterface.GetPermissions();
                        if (permissions != eHopperPermissions.AddAndRemove && permissions != permission)
                        {
                            this.mAttachedHoppers.RemoveAt(index);
                            int num = index - 1;
                            break;
                        }
                        break;
                    }
                    eHopperPermissions permissions1 = hopper.GetPermissions();
                    if (permissions1 != eHopperPermissions.AddAndRemove && permissions1 != permission)
                    {
                        hopper = (StorageMachineInterface)null;
                        break;
                    }
                }
            }
        }
        if (hopper == null)
            return;
        this.mAttachedHoppers.Add(hopper);
    }

    private void AddAttachedMassStorage(MassStorageCrate crate)
    {
        for (int index = 0; index < this.mAttachedMassStorage.Count; ++index)
        {
            MassStorageCrate massStorageCrate = this.mAttachedMassStorage[index];
            if (massStorageCrate != null)
            {
                if (massStorageCrate.mbDelete)
                {
                    this.mAttachedHoppers.RemoveAt(index);
                    --index;
                }
                else if (massStorageCrate == crate)
                    crate = (MassStorageCrate)null;
            }
        }
        if (crate == null)
            return;
        this.mAttachedMassStorage.Add(crate);
    }

    public string GetName()
    {
        return this.mType.ToString();
    }

    public static int GetMaxCargo(StockpileLiftMob.CargoLiftType type)
    {
        switch (type)
        {
            case StockpileLiftMob.CargoLiftType.Basic:
                return 300;
            case StockpileLiftMob.CargoLiftType.Improved:
                return 1000;
            case StockpileLiftMob.CargoLiftType.Bulk:
                return 3000;
            default:
                return 0;
        }
    }

    public void UpdateOrder()
    {
        if (this.mController == null)
            return;
        if (this.mDirection == StockpileLiftMob.Direction.Up)
            this.mCurrentLoadOrder = this.mController.GetTopOrder();
        else
            this.mCurrentLoadOrder = this.mController.GetBottomOrder();
    }

    public void SetData(StockpileLiftMob.StockpileLiftData data)
    {
        if (data.serverID >= 0)
            this.mnServerID = data.serverID;
        this.SetLiftType(data.type);
        this.mRenderOffset = data.renderOffset;
        this.mrSpeed = data.speed;
        this.mTargetHeight = data.targetHeight;
        this.mnUsedStorage = data.usedSpace;
        this.mOperatingState = data.operatingState;
        this.mLoadingState = data.loadingState;
        this.mDirection = data.direction;
        this.mCurrentLoadOrder = data.loadOrder;
        this.StockLimits = data.stocklimits;
        long num = this.mnY + (long)Mathf.FloorToInt(this.mRenderOffset + 0.5f);
        if (this.mOperatingState == StockpileLiftMob.OperatingState.Loading || this.mOperatingState == StockpileLiftMob.OperatingState.Unloading)
        {
            if (this.mTargetHeight == 0L)
            {
                Debug.LogError(("Error, attempted to set Lift into state " + this.mOperatingState + " but our target height isn't set!"));
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
            }
            if (num != this.mTargetHeight)
            {
                if (WorldScript.mbIsServer)
                    Debug.LogError(("Error, attempted to set Lift data into state " + this.mOperatingState + " but our height is " + (num - 4611686017890516992L) + " and our target height is " + (this.mTargetHeight - 4611686017890516992L)));
                this.mnY = this.mTargetHeight >> 4 << 4;
                this.mRenderOffset = (this.mTargetHeight % 16L) + 0.5f;
                this.Move(0.0f);
                this.SetNewOperatingState(StockpileLiftMob.OperatingState.Travelling);
            }
            this.SanityCheck();
        }
        if (data.cargo == null)
            return;
        this.mCargo.AddRange(data.cargo);
        this.StockLimits = data.stocklimits;
        this.CheckStorage();
    }

    private void CheckStorage()
    {
        int itemcount = this.mCargo.Sum(x =>
            {
                if (x != null)
                {
                    if (x.mType == ItemType.ItemStack)
                        return (x as ItemStack).mnAmount;
                    else if (x.mType == ItemType.ItemCubeStack)
                        return (x as ItemCubeStack).mnAmount;
                    else
                        return 1;
                }
                return 0;
            });
        if (this.mnUsedStorage != itemcount)
            this.mnUsedStorage = itemcount;
    }

    public Segment AttemptGetSegment(long x, long y, long z)
    {
        Segment segment;
        if (this.mFrustrum != null)
        {
            segment = this.mFrustrum.GetSegment(x, y, z);
            if (segment == null)
            {
                SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(this.mFrustrum, x, y, z);
                return (Segment)null;
            }
            if (!segment.mbInitialGenerationComplete || segment.mbDestroyed)
                return (Segment)null;
        }
        else
        {
            segment = WorldScript.instance.GetSegment(x, y, z);
            if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                return (Segment)null;
        }
        return segment;
    }

    public void GetCube(long lTestX, long lTestY, long lTestZ, out ushort lCube, out ushort lValue, out byte lFlags)
    {
        if (lTestX < 100000L)
            Debug.LogError(("Error, either you travelled 500 light years, or the mob is lost! X is " + lTestX));
        if (lTestY < 100000L)
            Debug.LogError(("Error, either you travelled 500 light years, or the mob is lost! Y is " + lTestY));
        if (lTestZ < 100000L)
            Debug.LogError(("Error, either you travelled 500 light years, or the mob is lost! Z is " + lTestZ));
        long segX;
        long segY;
        long segZ;
        WorldHelper.GetSegmentCoords(lTestX, lTestY, lTestZ, out segX, out segY, out segZ);
        if (this.mPrevGetSeg == null || this.mPrevGetSeg.baseX != segX || (this.mPrevGetSeg.baseY != segY || this.mPrevGetSeg.baseZ != segZ))
        {
            Segment segment = WorldScript.instance.GetSegment(segX, segY, segZ);
            if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
            {
                this.mPrevGetSeg = (Segment)null;
                lCube = (ushort)0;
                lValue = (ushort)0;
                lFlags = (byte)0;
                return;
            }
            this.mPrevGetSeg = segment;
        }
        lCube = this.mPrevGetSeg.GetCube(lTestX, lTestY, lTestZ);
        CubeData cubeData = this.mPrevGetSeg.GetCubeData(lTestX, lTestY, lTestZ);
        lValue = cubeData.mValue;
        lFlags = cubeData.meFlags;
    }

    public bool AddStockItem(ItemBase item)
    {
        if (this.StockLimits.Exists(x => x.Item.Compare(item)))
            return false;
        this.StockLimits.Add(new StockItem(item, 0));
        return true;
    }

    public bool RemoveStockItem(ItemBase item)
    {
        if (item == null)
        {
            Debug.LogWarning("RemoveRegistry tried to remove for null crate or item");
            return false;
        }
        this.StockLimits.RemoveAll(x => x.Item.Compare(item));
        return true;
    }

    public bool SetNetworkStock(ItemBase item, int stocklimit)
    {
        if (item != null && this.StockLimits.Exists(x => x.Item.Compare(item)))
        {
            if (stocklimit > this.mnMaxStorage)
                stocklimit = this.mnMaxStorage;
            else if (stocklimit < 0)
                stocklimit = 0;
            this.StockLimits.Where(x => x.Item.Compare(item)).FirstOrDefault().StockLimit = stocklimit;
            return true;
        }
        return false;
    }

    public bool NewNetworkStock(ItemBase item, bool removeitem)
    {
        if (removeitem)
            return this.RemoveStockItem(item);
        else if (item != null)
            return this.AddStockItem(item);
        return false;
    }

    public override int GetVersion()
    {
        return 0;
    }

    public override bool ShouldSave()
    {
        return false;
    }

    public override void WriteNetworkUpdate(BinaryWriter writer)
    {
        writer.Write((int)this.mType);
        writer.Write((int)this.mOperatingState);
        writer.Write((int)this.mDirection);
        writer.Write((int)this.mLoadingState);
        writer.Write((int)this.mCurrentLoadOrder);
        writer.Write(this.mbOutOfPower);
        writer.Write(this.mRenderOffset);
        writer.Write(this.mrSpeed);
        writer.Write(this.mTargetHeight);
        writer.Write(this.mnUsedStorage);
    }

    public override void ReadNetworkUpdate(BinaryReader reader, int version)
    {
        base.ReadNetworkUpdate(reader, version);
        this.mType = (StockpileLiftMob.CargoLiftType)reader.ReadInt32();
        this.mOperatingState = (StockpileLiftMob.OperatingState)reader.ReadInt32();
        this.mDirection = (StockpileLiftMob.Direction)reader.ReadInt32();
        this.mLoadingState = (StockpileLiftMob.LoadingState)reader.ReadInt32();
        this.mCurrentLoadOrder = (StockpileLiftMob.CargoLoadOrder)reader.ReadInt32();
        this.mbOutOfPower = reader.ReadBoolean();
        this.mRenderOffset = reader.ReadSingle();
        this.mrSpeed = reader.ReadSingle();
        this.mTargetHeight = reader.ReadInt64();
        this.mnUsedStorage = reader.ReadInt32();
        this.SetLiftType(this.mType);
    }

    public class StockpileLiftData
    {
        public int serverID;
        public StockpileLiftMob.OperatingState operatingState;
        public StockpileLiftMob.LoadingState loadingState;
        public StockpileLiftMob.Direction direction;
        public StockpileLiftMob.CargoLoadOrder loadOrder;
        public long x;
        public long y;
        public long z;
        public float renderOffset;
        public StockpileLiftMob.CargoLiftType type;
        public float speed;
        public long targetHeight;
        public int usedSpace;
        public List<ItemBase> cargo;
        public List<StockItem> stocklimits;
    }

    public enum OperatingState
    {
        Travelling,
        Loading,
        Unloading,
        Stuck,
    }

    public enum Direction
    {
        Up,
        Down,
    }

    public enum LoadingState
    {
        AcquiringInventories,
        MovingItems,
        CleanUp,
    }

    public enum CargoLiftType
    {
        Basic,
        Improved,
        Bulk,
    }

    public enum CargoLoadOrder
    {
        UnloadAny,
        UnloadAll,
        LoadAny,
        LoadAll,
    }
}
