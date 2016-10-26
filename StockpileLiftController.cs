using System;
using System.Collections.Generic;
using FortressCraft.Community.Utilities;
using UnityEngine;
using System.IO;


public class StockpileLiftController : MachineEntity, PowerConsumerInterface
{
    public string FriendlyState = "Unknown state!";
    public float mrMaxPower = 3000f;
    public float mrMaxTransferRate = 300f;
    public float mrPowerUsePerSecond = 100f;
    public List<StorageMachineInterface> mAttachedHoppers = new List<StorageMachineInterface>();
    public StockpileLiftMob.CargoLoadOrder mUpperLoadOrder = StockpileLiftMob.CargoLoadOrder.UnloadAll;
    public StockpileLiftMob.CargoLoadOrder mLowerLoadOrder = StockpileLiftMob.CargoLoadOrder.LoadAll;
    public float mRailCheckTimer = 600f;
    public static ushort PLACEMENT_VALUE = ModManager.mModMappings.CubesByKey["MachinePlacement"].ValuesByKey["steveman0.StockLiftPlacement"].Value;
    public static ushort COMPONENT_VALUE = ModManager.mModMappings.CubesByKey["steveman0.StockpileLiftController"].ValuesByKey["steveman0.StockpileLiftControllerBlock"].Value;
    public static ushort CENTER_VALUE = ModManager.mModMappings.CubesByKey["steveman0.StockpileLiftController"].ValuesByKey["steveman0.StockpileLiftControllerCenter"].Value;
    public const string SHORT_NAME = "CARGO LIFT CONTROLLER";
    public static ushort PLACEMENT_TYPE = ModManager.mModMappings.CubesByKey["MachinePlacement"].CubeType;
    public static ushort CUBE_TYPE = ModManager.mModMappings.CubesByKey["steveman0.StockpileLiftController"].CubeType;
    public const SpawnableObjectEnum SPAWNABLE_OBJECT = SpawnableObjectEnum.CargoLiftController;
    public const eSegmentEntity SEGMENT_ENTITY = eSegmentEntity.Mod;
    private const int MB_X = 3;
    private const int MB_Y = 1;
    private const int MB_Z = 3;
    private const int MB_MIN_X = -1;
    private const int MB_MIN_Y = 0;
    private const int MB_MIN_Z = -1;
    private const int MB_MAX_X = 1;
    private const int MB_MAX_Y = 0;
    private const int MB_MAX_Z = 1;
    private const int MB_OUTER_X = 2;
    private const int MB_OUTER_Y = 0;
    private const int MB_OUTER_Z = 2;
    public const ushort RAIL_BLOCK = 602;
    public const float RAIL_TIMER = 1f;
    public const float RAIL_CHECK_TIMER = 600f;
    public const float RAIL_BUILD_POWER = 100f;
    public const int RAIL_BUFFER_SIZE = 2;
    public bool mbIsCenter;
    public StockpileLiftController mLinkedCenter;
    public MachineEntity.MBMState mMBMState;
    public long mLinkX;
    public long mLinkY;
    public long mLinkZ;
    public float mrCurrentPower;
    private int mnHopperRoundRobinPosition;
    public int mnCurrentSideIndex;
    public int mnCurrentSide;
    public long mnTargetDepth;
    public StockpileLiftController.PowerState mPowerState;
    public StockpileLiftController.OperatingState mOperatingState;
    public StockpileLiftController.RailState mRailState;
    public float mRailTimer;
    public long mnRailDepth;
    public long mnRailCheckDepth;
    public int mnRailCount;
    public ItemBase mLiftItem;
    public StockpileLiftMob mLiftMob;
    public bool mbDeployLift;
    public StockpileLiftMob.StockpileLiftData mSpawnData;
    private int mnUpdates;
    private Renderer mRend;
    private bool mbLinkedToGO;
    private MaterialPropertyBlock mMPB;
    private Color mCurrentGlowCol;
    private float mrGlow;

    private bool LiftNeedsLinked = false;

    //public StockpileLiftControllerWindow MachineWindow = new StockpileLiftControllerWindow();

    public StockpileLiftController(ModCreateSegmentEntityParameters parameters)
    : base(parameters)
  {
        this.mMBMState = MachineEntity.MBMState.WaitingForLink;
        if ((int)parameters.Value != CENTER_VALUE)
            return;
        this.mbIsCenter = true;
        this.mMBMState = MachineEntity.MBMState.ReacquiringLink;
        this.RequestLowFrequencyUpdates();
        this.mbNeedsUnityUpdate = true;
        this.SetNewOperatingState(StockpileLiftController.OperatingState.InstallingRails);
    }

    private void DeconstructMachineFromCentre(StockpileLiftController deletedBlock)
    {
        Debug.LogWarning("Deconstructing CARGO LIFT CONTROLLER into placement blocks");
        for (int index1 = 0; index1 <= 0; ++index1)
        {
            for (int index2 = -1; index2 <= 1; ++index2)
            {
                for (int index3 = -1; index3 <= 1; ++index3)
                {
                    long x = this.mnX + (long)index3;
                    long y = this.mnY + (long)index1;
                    long z = this.mnZ + (long)index2;
                    if ((index3 != 0 || index1 != 0 || index2 != 0) && (deletedBlock == null || x != deletedBlock.mnX || (y != deletedBlock.mnY || z != deletedBlock.mnZ)))
                    {
                        Segment segment = WorldScript.instance.GetSegment(x, y, z);
                        if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                        {
                            this.mMBMState = MachineEntity.MBMState.Delinking;
                            this.RequestLowFrequencyUpdates();
                            return;
                        }
                        if ((int)segment.GetCube(x, y, z) == CUBE_TYPE)
                        {
                            StockpileLiftController cargoLiftController = segment.FetchEntity(eSegmentEntity.Mod, x, y, z) as StockpileLiftController;

                            if (cargoLiftController == null)
                                Debug.LogWarning("Failed to refind a STOCKPILE LIFT CONTROLLER entity? wut?");
                            else
                                cargoLiftController.DeconstructSingleBlock();
                        }
                    }
                }
            }
        }
        if (this == deletedBlock)
            return;
        this.DeconstructSingleBlock();
    }

    private void DeconstructSingleBlock()
    {
        this.mMBMState = MachineEntity.MBMState.Delinked;
        WorldScript.instance.BuildFromEntity(this.mSegment, this.mnX, this.mnY, this.mnZ, PLACEMENT_TYPE, PLACEMENT_VALUE);
    }
    
    public void LinkMultiBlockMachine()
    {
        for (int index1 = 0; index1 <= 0; ++index1)
        {
            for (int index2 = -1; index2 <= 1; ++index2)
            {
                for (int index3 = -1; index3 <= 1; ++index3)
                {
                    long x = this.mnX + (long)index3;
                    long y = this.mnY + (long)index1;
                    long z = this.mnZ + (long)index2;
                    if (index3 != 0 || index1 != 0 || index2 != 0)
                    {
                        Segment segment = this.AttemptGetSegment(x, y, z);
                        if (segment == null)
                            return;
                        if ((int)segment.GetCube(x, y, z) == CUBE_TYPE)
                        {
                            StockpileLiftController cargoLiftController = segment.FetchEntity(eSegmentEntity.Mod, x, y, z) as StockpileLiftController;
                            if (cargoLiftController == null)
                                return;
                            if (cargoLiftController.mMBMState != MachineEntity.MBMState.Linked || cargoLiftController.mLinkedCenter != this)
                            {
                                if (cargoLiftController.mMBMState != MachineEntity.MBMState.ReacquiringLink || cargoLiftController.mLinkX != this.mnX || (cargoLiftController.mLinkY != this.mnY || cargoLiftController.mLinkZ == this.mnZ))
                                    ;
                                cargoLiftController.mMBMState = MachineEntity.MBMState.Linked;
                                cargoLiftController.AttachToCentreBlock(this);
                            }
                        }
                        else
                            Debug.LogWarning("Found badly formed CARGO LIFT CONTROLLER?");
                    }
                }
            }
        }
        this.ContructionFinished();
        this.DropExtraSegments((Segment)null);
    }

    private void ContructionFinished()
    {
        this.FriendlyState = "CARGO LIFT CONTROLLER Constructed!";
        this.mMBMState = MachineEntity.MBMState.Linked;
        this.mSegment.RequestRegenerateGraphics();
        this.MarkDirtyDelayed();
    }

    private void AttachToCentreBlock(StockpileLiftController centerBlock)
    {
        if (centerBlock == null)
            Debug.LogError("Error, can't set side - requested centre is null!");
        this.mMBMState = MachineEntity.MBMState.Linked;
        if (this.mLinkX != centerBlock.mnX)
        {
            this.MarkDirtyDelayed();
            this.mSegment.RequestRegenerateGraphics();
        }
        this.mLinkedCenter = centerBlock;
        this.mLinkX = centerBlock.mnX;
        this.mLinkY = centerBlock.mnY;
        this.mLinkZ = centerBlock.mnZ;
    }

    private static int GetExtents(int x, int y, int z, long lastX, long lastY, long lastZ, WorldFrustrum frustrum)
    {
        long checkX = lastX;
        long checkY = lastY;
        long checkZ = lastZ;
        int num = 0;
        for (int index = 0; index < 100; ++index)
        {
            checkX += (long)x;
            checkY += (long)y;
            checkZ += (long)z;
            if (StockpileLiftController.IsCubeThisMachine(checkX, checkY, checkZ, frustrum))
                ++num;
            else
                break;
        }
        return num;
    }

    private static bool IsCubeThisMachine(long checkX, long checkY, long checkZ, WorldFrustrum frustrum)
    {
        Segment segment = frustrum.GetSegment(checkX, checkY, checkZ);
        return segment != null && segment.mbInitialGenerationComplete && (!segment.mbDestroyed && (int)segment.GetCube(checkX, checkY, checkZ) == PLACEMENT_TYPE) && (int)segment.GetCubeData(checkX, checkY, checkZ).mValue == PLACEMENT_VALUE;
    }
    public static void CheckForCompletedMachine(WorldFrustrum frustrum, long lastX, long lastY, long lastZ)
    {
        int num1 = StockpileLiftController.GetExtents(-1, 0, 0, lastX, lastY, lastZ, frustrum) + StockpileLiftController.GetExtents(1, 0, 0, lastX, lastY, lastZ, frustrum) + 1;
        if (3 > num1)
        {
            Debug.LogWarning(("CARGO LIFT CONTROLLER isn't big enough along X(" + num1 + ")"));
        }
        else
        {
            if (3 > num1)
                return;
            int num2 = StockpileLiftController.GetExtents(0, -1, 0, lastX, lastY, lastZ, frustrum) + StockpileLiftController.GetExtents(0, 1, 0, lastX, lastY, lastZ, frustrum) + 1;
            if (1 > num2)
            {
                Debug.LogWarning(("CARGO LIFT CONTROLLER isn't big enough along Y(" + num2 + ")"));
            }
            else
            {
                if (1 > num2)
                    return;
                int num3 = StockpileLiftController.GetExtents(0, 0, -1, lastX, lastY, lastZ, frustrum) + StockpileLiftController.GetExtents(0, 0, 1, lastX, lastY, lastZ, frustrum) + 1;
                if (3 > num3)
                {
                    Debug.LogWarning(("CARGO LIFT CONTROLLER isn't big enough along Z(" + num3 + ")"));
                }
                else
                {
                    if (3 > num3)
                        return;
                    Debug.LogWarning(("CARGO LIFT CONTROLLER is detecting test span of " + num1 + ":" + num2 + ":" + num3));
                    bool[,,] flagArray = new bool[3, 1, 3];
                    for (int index1 = 0; index1 <= 0; ++index1)
                    {
                        for (int index2 = -1; index2 <= 1; ++index2)
                        {
                            for (int index3 = -1; index3 <= 1; ++index3)
                                flagArray[index3 + 1, index1, index2 + 1] = true;
                        }
                    }
                    for (int index1 = 0; index1 <= 0; ++index1)
                    {
                        for (int index2 = -2; index2 <= 2; ++index2)
                        {
                            for (int index3 = -2; index3 <= 2; ++index3)
                            {
                                if (index3 != 0 || index1 != 0 || index2 != 0)
                                {
                                    Segment segment = frustrum.GetSegment(lastX + index3, lastY + index1, lastZ + index2);
                                    if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                                        return;
                                    ushort cube = segment.GetCube(lastX + index3, lastY + index1, lastZ + index2);
                                    bool flag = false;
                                    if (cube == PLACEMENT_TYPE && segment.GetCubeData(lastX + index3, lastY + index1, lastZ + index2).mValue == PLACEMENT_VALUE)
                                        flag = true;
                                    if (!flag)
                                    {
                                        for (int index4 = 0; index4 <= 0; ++index4)
                                        {
                                            for (int index5 = -1; index5 <= 1; ++index5)
                                            {
                                                for (int index6 = -1; index6 <= 1; ++index6)
                                                {
                                                    int num4 = index3 + index6;
                                                    int index7 = index1 + index4;
                                                    int num5 = index2 + index5;
                                                    if (num4 >= -1 && num4 <= 1 && (index7 >= 0 && index7 <= 0) && (num5 >= -1 && num5 <= 1))
                                                        flagArray[num4 + 1, index7, num5 + 1] = false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    int num6 = 0;
                    for (int index1 = 0; index1 <= 0; ++index1)
                    {
                        for (int index2 = -1; index2 <= 1; ++index2)
                        {
                            for (int index3 = -1; index3 <= 1; ++index3)
                            {
                                if (flagArray[index3 + 1, index1, index2 + 1])
                                    ++num6;
                            }
                        }
                    }
                    if (num6 > 1)
                    {
                        Debug.LogWarning(("Warning, OE has too many valid positions (" + num6 + ")"));
                    }
                    else
                    {
                        if (num6 == 0)
                            return;
                        for (int index1 = 0; index1 <= 0; ++index1)
                        {
                            for (int index2 = -1; index2 <= 1; ++index2)
                            {
                                for (int index3 = -1; index3 <= 1; ++index3)
                                {
                                    if (flagArray[index3 + 1, index1, index2 + 1])
                                    {
                                        if (StockpileLiftController.BuildMultiBlockMachine(frustrum, lastX + index3, lastY + index1, lastZ + index2))
                                            return;
                                        Debug.LogError("Error, failed to build CARGO LIFT CONTROLLER due to bad segment?");
                                    }
                                }
                            }
                        }
                        if (num6 == 0)
                            return;
                        Debug.LogError("Error, thought we found a valid position, but failed to build the CARGO LIFT CONTROLLER?");
                    }
                }
            }
        }
    }

    public static bool BuildMultiBlockMachine(WorldFrustrum frustrum, long centerX, long centerY, long centerZ)
    {
        HashSet<Segment> hashSet = new HashSet<Segment>();
        bool flag = true;
        try
        {
            WorldScript.mLocalPlayer.mResearch.GiveResearch(CUBE_TYPE, 0);
            WorldScript.mLocalPlayer.mResearch.GiveResearch(CUBE_TYPE, 1);
            for (int index1 = 0; index1 <= 0; ++index1)
            {
                for (int index2 = -1; index2 <= 1; ++index2)
                {
                    for (int index3 = -1; index3 <= 1; ++index3)
                    {
                        Segment segment = frustrum.GetSegment(centerX + (long)index3, centerY + (long)index1, centerZ + (long)index2);
                        if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                        {
                            flag = false;
                        }
                        else
                        {
                            if (!hashSet.Contains(segment))
                            {
                                hashSet.Add(segment);
                                segment.BeginProcessing();
                            }
                            if (index3 == 0 && index1 == 0 && index2 == 0)
                                frustrum.BuildOrientation(segment, centerX + (long)index3, centerY + (long)index1, centerZ + (long)index2, CUBE_TYPE, CENTER_VALUE, (byte)65);
                            else
                                frustrum.BuildOrientation(segment, centerX + (long)index3, centerY + (long)index1, centerZ + (long)index2, CUBE_TYPE, COMPONENT_VALUE, (byte)65);
                        }
                    }
                }
            }
        }
        finally
        {
            using (HashSet<Segment>.Enumerator enumerator = hashSet.GetEnumerator())
            {
                while (enumerator.MoveNext())
                    enumerator.Current.EndProcessing();
            }
            WorldScript.instance.mNodeWorkerThread.KickNodeWorkerThread();
        }
        if (!flag)
            Debug.LogError("Error, failed to build CARGO LIFT CONTROLLER as one of it's segments wasn't valid!");
        else
            AudioSpeechManager.PlayStructureCompleteDelayed = true;
        return flag;
    }

    public override void OnDelete()
    {
        base.OnDelete();
        if (this.mbIsCenter)
        {
            if (this.mLiftMob != null)
            {
                MobManager.instance.DestroyMob((MobEntity)this.mLiftMob);
                this.mLiftMob = (StockpileLiftMob)null;
            }
            if (this.mLiftItem != null)
            {
                ItemManager.instance.DropItem(this.mLiftItem, this.mnX, this.mnY, this.mnZ, Vector3.zero);
                this.mLiftItem = (ItemBase)null;
            }
        }
        if (this.mMBMState == MachineEntity.MBMState.Linked)
        {
            if (WorldScript.mbIsServer)
                ItemManager.DropNewCubeStack(PLACEMENT_TYPE, PLACEMENT_VALUE, 1, this.mnX, this.mnY, this.mnZ, Vector3.zero);
            this.mMBMState = MachineEntity.MBMState.Delinking;
            if (this.mbIsCenter)
                this.DeconstructMachineFromCentre(this);
            else if (this.mLinkedCenter == null)
                Debug.LogWarning("Error, CARGO LIFT CONTROLLER had no linked centre, so cannot destroy linked centre?");
            else
                this.mLinkedCenter.DeconstructMachineFromCentre(this);
        }
        else
        {
            if (this.mMBMState == MachineEntity.MBMState.Delinked)
                return;
            Debug.LogWarning((object)("Deleted CARGO LIFT CONTROLLER while in state " + (object)this.mMBMState));
        }
    }

    public override void SpawnGameObject()
    {
        this.mObjectType = SpawnableObjectEnum.CargoLiftController;
        if (!this.mbIsCenter)
            return;
        base.SpawnGameObject();
    }

    public override void DropGameObject()
    {
        base.DropGameObject();
        this.mbLinkedToGO = false;
    }

    public override void LowFrequencyUpdate()
    {
        if (!this.mbIsCenter)
            return;
        //if (this.mLiftItem != null && this.mLiftMob == null)
        //    this.RequestImmediateNetworkUpdate();
        this.UpdatePlayerDistanceInfo();
        if (this.mMBMState == MachineEntity.MBMState.ReacquiringLink)
            this.LinkMultiBlockMachine();
        if (this.mMBMState == MachineEntity.MBMState.Delinking)
            this.DeconstructMachineFromCentre(null);
        this.LookForMachines();
        this.UpdateState();
        ++this.mnUpdates;
    }

    public override void UnityUpdate()
    {
        //if (this.mbIsCenter)
        //    UIUtil.DisconnectUI(this);
        if (this.mLiftMob != null && mLiftMob.mWrapper != null && this.LiftNeedsLinked && mLiftMob.mWrapper.mGameObjectList != null && mLiftMob.mWrapper.mGameObjectList.Count != 0)
        {
            Component[] obj = mLiftMob.mWrapper.mGameObjectList[0].gameObject.GetComponentsInChildren(typeof(Component));
            foreach (Component x in obj)
            {
                //Debug.Log("mWrapper Object: " + x + " name: " + x.name);
                if (x.name == "CargoLift_Unity")
                    x.gameObject.SetActive(false);
            }
            mLiftMob.mWrapper.mGameObjectList[0].AddComponent<StockpileLiftUnity>();
            mLiftMob.mWrapper.mGameObjectList[0].gameObject.SetActive(true);
            this.LiftNeedsLinked = false;
        }
        if (!this.mbIsCenter)
            return;
        if (!this.mbLinkedToGO)
        {
            if (this.mWrapper == null || !this.mWrapper.mbHasGameObject)
                return;
            if (this.mWrapper.mGameObjectList == null)
                Debug.LogError("PSB missing game object #0?");
            if (this.mWrapper.mGameObjectList[0].gameObject == null)
                Debug.LogError("PSB missing game object #0 (GO)?");
            this.mRend = Extensions.Search(this.mWrapper.mGameObjectList[0].transform, "CargoLiftTop").GetComponent<Renderer>();
            this.mMPB = new MaterialPropertyBlock();
            this.mCurrentGlowCol = Color.black;
            this.mbLinkedToGO = true;
            this.mrGlow = 0.0f;
        }
        float num = 4f;
        Color b = Color.black;
        if (this.mLiftMob != null)
        {
            if (this.mLiftMob.mType == StockpileLiftMob.CargoLiftType.Basic)
                b = Color.green;
            if (this.mLiftMob.mType == StockpileLiftMob.CargoLiftType.Improved)
                b = new Color(0.75f, 0.1f, 1f);
            if (this.mLiftMob.mType == StockpileLiftMob.CargoLiftType.Bulk)
                b = new Color(1f, 0.75f, 0.1f);
            if (this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Unloading)
                num = 2f;
            if (this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Loading)
                num = 2f;
            if (this.mPowerState == StockpileLiftController.PowerState.OutOfPower)
                num = 0.25f;
        }
        else
            num = 0.0f;
        this.mCurrentGlowCol = Color.Lerp(this.mCurrentGlowCol, b, Time.deltaTime);
        this.mMPB.SetColor("_GlowColor", this.mCurrentGlowCol);
        this.mrGlow += (num - this.mrGlow) * Time.deltaTime;
        this.mMPB.SetFloat("_GlowMult", this.mrGlow);
        this.mRend.SetPropertyBlock(this.mMPB);
    }

    private void RequestLowFrequencyUpdates()
    {
        if (this.mbNeedsLowFrequencyUpdate)
            return;
        this.mbNeedsLowFrequencyUpdate = true;
        this.mSegment.mbNeedsLowFrequencyUpdate = true;
        if (this.mSegment.mbIsQueuedForUpdate)
            return;
        WorldScript.instance.mSegmentUpdater.AddSegment(this.mSegment);
    }

    private void UpdateState()
    {
        if (this.mMBMState != MachineEntity.MBMState.Linked)
            return;
        if ((double)this.mrCurrentPower < 0.0)
            this.mrCurrentPower = 0.0f;
        if (this.mPowerState == StockpileLiftController.PowerState.OutOfPower)
        {
            if ((double)this.mrCurrentPower < (double)this.GetRequiredPower())
                return;
            this.mPowerState = StockpileLiftController.PowerState.Powered;
            this.RequestImmediateNetworkUpdate();
        }
        else if ((double)this.mrCurrentPower < (double)this.GetRequiredPower())
        {
            this.mPowerState = StockpileLiftController.PowerState.OutOfPower;
            this.RequestImmediateNetworkUpdate();
        }
        else
        {
            if (!WorldScript.mbIsServer)
                return;
            switch (this.mOperatingState)
            {
                case StockpileLiftController.OperatingState.InstallingRails:
                    this.UpdateInstallingRails();
                    break;
                case StockpileLiftController.OperatingState.Operating:
                    if (this.mnRailDepth < 6L)
                    {
                        Debug.LogError("Error, lift was in Operating state, but had a shaft length <5 - rechecking!");
                        this.ResetRailCheck();
                        return;
                    }
                    this.UpdateOperating();
                    break;
                case StockpileLiftController.OperatingState.Checking:
                    this.UpdateOperating();
                    this.UpdateRailCheck();
                    break;
            }
            if (this.mLiftMob == null)
                return;
            this.mrCurrentPower -= this.GetRequiredPower();
            this.mLiftMob.LowFrequencyUpdate();
            float depth = this.GetDepth(this.mLiftMob.mnY, this.mLiftMob.mRenderOffset);
            if ((double)depth <= -2.0)
                return;
            this.mLiftMob.mTargetHeight = this.GetTopCoord();
            this.mLiftMob.mRenderOffset = 0.0f;
            this.mLiftMob.mnY = this.mnY - 2L;
            Debug.LogError(("Error, loaded in CargoLift Mob at height " + (float)((double)this.mLiftMob.mnY + (double)this.mLiftMob.mRenderOffset - 4.61168601842739E+18) + " but the CargoLift Controller was at height " + (this.mnY - 4611686017890516992L) + " fixing depth of " + depth));
        }
    }

    private float GetRequiredPower()
    {
        if (this.mOperatingState == StockpileLiftController.OperatingState.InstallingRails)
            return 100f;
        if ((this.mOperatingState == StockpileLiftController.OperatingState.Operating || this.mOperatingState == StockpileLiftController.OperatingState.Checking) && (this.mLiftMob == null || this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Loading || this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Unloading))
            return 0.0f;
        float num = 0.0f;
        if (this.mLiftMob.mType == StockpileLiftMob.CargoLiftType.Basic)
            num = 35f;
        if (this.mLiftMob.mType == StockpileLiftMob.CargoLiftType.Improved)
            num = 250f;
        if (this.mLiftMob.mType == StockpileLiftMob.CargoLiftType.Bulk)
            num = 850f;
        return num * LowFrequencyThread.mrPreviousUpdateTimeStep;
    }

    private void UpdateInstallingRails()
    {
        if (this.mRailState == StockpileLiftController.RailState.WaitingForResources)
        {
            this.CheckForAvailableRails();
        }
        else
        {
            if (this.mRailState == StockpileLiftController.RailState.Blocked)
                return;
            long y = this.mnY - this.mnRailDepth - 1L;
            long num = this.mnY - this.mnRailDepth;
            if (y >> 4 == num >> 4)
                ;
            if (this.CheckRailArea(this.mnX, y, this.mnZ))
                return;
            this.mRailState = StockpileLiftController.RailState.Scanning;
        }
    }

    private void CheckForAvailableRails()
    {
        int a = 0;
        for (int index = 0; index < this.mAttachedHoppers.Count; ++index)
        {
            StorageMachineInterface machineInterface = this.mAttachedHoppers[index];
            if (machineInterface != null)
            {
                if (((SegmentEntity)machineInterface).mbDelete)
                {
                    this.mAttachedHoppers.RemoveAt(index);
                    --index;
                }
                else
                {
                    int num = machineInterface.CountCubes((ushort)602, (ushort)0);
                    if (num > 0)
                        a += num;
                }
            }
        }
        if (a <= 0)
            return;
        int railCount = Mathf.Min(a, 2 - this.mnRailCount);
        this.RemoveRails(railCount);
        this.mnRailCount += railCount;
        this.mRailState = StockpileLiftController.RailState.Scanning;
        this.MarkDirtyDelayed();
        this.RequestImmediateNetworkUpdate();
    }

    private void RemoveRails(int railCount)
    {
        for (int index = 0; index < this.mAttachedHoppers.Count; ++index)
        {
            StorageMachineInterface machineInterface = this.mAttachedHoppers[index];
            if (machineInterface != null)
            {
                if (((SegmentEntity)machineInterface).mbDelete)
                {
                    this.mAttachedHoppers.RemoveAt(index);
                    --index;
                }
                else
                {
                    int cubes = machineInterface.TryPartialExtractCubes((StorageUserInterface)this, (ushort)602, (ushort)0, railCount);
                    if (cubes > 0)
                    {
                        railCount -= cubes;
                        if (railCount <= 0)
                            break;
                    }
                }
            }
        }
    }

    private bool CheckRailArea(long x, long y, long z)
    {
        bool flag1 = true;
        for (int index1 = -1; index1 < 2; ++index1)
        {
            for (int index2 = -1; index2 < 2; ++index2)
            {
                Segment segment = this.AttemptGetSegment(x + (long)index1, y, z + (long)index2);
                if (segment == null)
                    return false;
                ushort cube = segment.GetCube(x + (long)index1, y, z + (long)index2);
                if (cube != 602 && !CubeHelper.IsTypeConsideredPassable(cube))
                {
                    flag1 = false;
                    break;
                }
            }
        }
        if (flag1)
        {
            Segment segment1 = this.AttemptGetSegment(x, y, z - 1L);
            Segment segment2 = this.AttemptGetSegment(x, y, z + 1L);
            if (segment1 == null || segment2 == null)
                return false;
            ushort cube1 = segment1.GetCube(x, y, z - 1L);
            ushort cube2 = segment2.GetCube(x, y, z + 1L);
            bool flag2 = false;
            if ((int)cube1 != 602)
            {
                if (this.mnRailCount > 0)
                {
                    WorldScript.instance.BuildOrientationFromEntity(segment1, x, y, z - 1L, (ushort)602, (ushort)0, (byte)4);
                    --this.mnRailCount;
                }
                else
                    flag2 = true;
            }
            if ((int)cube2 != 602)
            {
                if (this.mnRailCount > 0)
                {
                    WorldScript.instance.BuildOrientationFromEntity(segment2, x, y, z + 1L, (ushort)602, (ushort)0, (byte)8);
                    --this.mnRailCount;
                }
                else
                    flag2 = true;
            }
            if (!flag2)
            {
                if (this.mOperatingState == StockpileLiftController.OperatingState.InstallingRails)
                    ++this.mnRailDepth;
                else
                    ++this.mnRailCheckDepth;
            }
            if (((int)cube1 != 602 || (int)cube2 != 602) && this.mnRailCount == 0)
                this.mRailState = StockpileLiftController.RailState.WaitingForResources;
            this.mrCurrentPower -= this.GetRequiredPower();
        }
        else
            this.mRailState = StockpileLiftController.RailState.Blocked;
        return true;
    }

    private void UpdateRailCheck()
    {
        if (this.mRailState == StockpileLiftController.RailState.WaitingForResources)
        {
            this.CheckForAvailableRails();
            this.mRailState = StockpileLiftController.RailState.Scanning;
        }
        else if (this.mRailState == StockpileLiftController.RailState.Blocked)
        {
            this.FinaliseCheck();
        }
        else
        {
            long y = this.mnY - this.mnRailCheckDepth - 1L;
            long num = this.mnY - this.mnRailCheckDepth;
            if (y >> 4 == num >> 4)
                ;
            if (this.CheckRailArea(this.mnX, y, this.mnZ))
                return;
            this.mRailState = StockpileLiftController.RailState.Scanning;
        }
    }

    private void FinaliseCheck()
    {
        if (this.mnRailCheckDepth < 6L)
        {
            Debug.LogError((object)("Error, Liftcontroller finalised check but depths were only " + this.mnRailCheckDepth + "/" + this.mnRailDepth + "!"));
            if (this.mLiftMob != null)
                this.mLiftMob.SetStuck();
            this.mnRailDepth = this.mnRailCheckDepth;
            this.ResetRailCheck();
            this.MarkDirtyDelayed();
            this.RequestImmediateNetworkUpdate();

        }
        else
        {
            if (this.mnRailCheckDepth == this.mnRailDepth && this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Stuck && (double)((float)(this.mnY - this.mLiftMob.mnY) - this.mLiftMob.mRenderOffset) <= (double)(this.mnRailCheckDepth + 2L))
                this.mLiftMob.ResumeOrder();
            if (this.mnRailCheckDepth > this.mnRailDepth)
            {
                long bottomCoord = this.GetBottomCoord();
                this.mnRailDepth = this.mnRailCheckDepth;
                if (this.mLiftMob != null)
                {
                    if (this.mLiftMob.mTargetHeight <= bottomCoord)
                        this.mLiftMob.mTargetHeight = this.GetBottomCoord();
                    if (this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Stuck && (double)((float)this.mnY - ((float)this.mLiftMob.mnY + this.mLiftMob.mRenderOffset)) <= (double)this.mnRailCheckDepth)
                        this.mLiftMob.ResumeOrder();
                }
            }
            if (this.mnRailCheckDepth < this.mnRailDepth)
            {
                long bottomCoord = this.GetBottomCoord();
                this.mnRailDepth = this.mnRailCheckDepth;
                if (this.mLiftMob != null)
                {
                    float num = (float)(this.mnY - this.mLiftMob.mnY) - this.mLiftMob.mRenderOffset;
                    Debug.Log((object)string.Concat(new object[4]
                    {
            (object) "Lift depth: ",
            (object) num,
            (object) " max depth: ",
            (object) (this.mnRailCheckDepth - 1L)
                    }));
                    if ((double)num > (double)(this.mnRailCheckDepth + 2L))
                        this.mLiftMob.SetStuck();
                    else if (this.mLiftMob.mTargetHeight == bottomCoord)
                        this.mLiftMob.mTargetHeight = this.GetBottomCoord();
                }
            }
            this.MarkDirtyDelayed();
            this.RequestImmediateNetworkUpdate();
            this.SetNewOperatingState(StockpileLiftController.OperatingState.Operating);
        }
    }

    private void UpdateOperating()
    {
        if (this.mbDeployLift && this.DeployLift())
            this.mbDeployLift = false;
        if (WorldScript.mbIsServer)
        {
            this.mRailCheckTimer -= LowFrequencyThread.mrPreviousUpdateTimeStep;
            if ((double)this.mRailCheckTimer <= 0.0)
                this.ResetRailCheck();
        }
        if (this.mLiftMob == null || this.mLiftMob.mOperatingState != StockpileLiftMob.OperatingState.Stuck)
            return;
        this.SetNewOperatingState(StockpileLiftController.OperatingState.Checking);
    }

    private void RoundRobinSide(out int y, out int x, out int z)
    {
        bool flag = false;
        if (this.mnCurrentSide == 0)
        {
            y = this.mnCurrentSideIndex / 3;
            x = -2;
            z = this.mnCurrentSideIndex % 3 - 1;
            ++this.mnCurrentSideIndex;
            if (this.mnCurrentSideIndex == 3)
                flag = true;
        }
        else if (this.mnCurrentSide == 1)
        {
            y = this.mnCurrentSideIndex / 3;
            x = 2;
            z = this.mnCurrentSideIndex % 3 - 1;
            ++this.mnCurrentSideIndex;
            if (this.mnCurrentSideIndex == 3)
                flag = true;
        }
        else if (this.mnCurrentSide == 2)
        {
            y = -1;
            x = this.mnCurrentSideIndex / 3 - 1;
            z = this.mnCurrentSideIndex % 3 - 1;
            ++this.mnCurrentSideIndex;
            if (this.mnCurrentSideIndex == 9)
                flag = true;
        }
        else if (this.mnCurrentSide == 3)
        {
            y = 1;
            x = this.mnCurrentSideIndex / 3 - 1;
            z = this.mnCurrentSideIndex % 3 - 1;
            ++this.mnCurrentSideIndex;
            if (this.mnCurrentSideIndex == 9)
                flag = true;
        }
        else if (this.mnCurrentSide == 4)
        {
            y = this.mnCurrentSideIndex / 3;
            x = this.mnCurrentSideIndex % 3 - 1;
            z = -2;
            ++this.mnCurrentSideIndex;
            if (this.mnCurrentSideIndex == 3)
                flag = true;
        }
        else
        {
            y = this.mnCurrentSideIndex / 3;
            x = this.mnCurrentSideIndex % 3 - 1;
            z = 2;
            ++this.mnCurrentSideIndex;
            if (this.mnCurrentSideIndex == 3)
                flag = true;
        }
        if (!flag)
            return;
        this.mnCurrentSideIndex = 0;
        this.mnCurrentSide = (this.mnCurrentSide + 1) % 6;
    }

    private void LookForMachines()
    {
        int y1;
        int x1;
        int z1;
        this.RoundRobinSide(out y1, out x1, out z1);
        long x2 = (long)x1 + this.mnX;
        long y2 = (long)y1 + this.mnY;
        long z2 = (long)z1 + this.mnZ;
        Segment segment = this.AttemptGetSegment(x2, y2, z2);
        if (segment == null)
            return;
        SegmentEntity segmentEntity = segment.SearchEntity(x2, y2, z2);
        if (!(segmentEntity is StorageMachineInterface))
            return;
        this.AddAttachedHopper((StorageMachineInterface)segmentEntity);
    }

    private void AddAttachedHopper(StorageMachineInterface hopper)
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
                else if (machineInterface == hopper)
                    hopper = (StorageMachineInterface)null;
            }
        }
        if (hopper == null)
            return;
        this.mAttachedHoppers.Add(hopper);
    }

    public long GetNextOrder(long previousHeight)
    {
        if (this.mOperatingState != StockpileLiftController.OperatingState.Operating)
            return 0;
        if (previousHeight == this.GetTopCoord())
            return this.GetBottomCoord();
        return this.GetTopCoord();
    }

    public long GetTopCoord()
    {
        return this.mnY - 2L;
    }

    public long GetBottomCoord()
    {
        return this.mnY - this.mnRailDepth + 1L;
    }

    public StockpileLiftMob.CargoLoadOrder GetTopOrder()
    {
        return this.mUpperLoadOrder;
    }

    public StockpileLiftMob.CargoLoadOrder GetBottomOrder()
    {
        return this.mLowerLoadOrder;
    }

    public bool IsValidLift(ItemBase swapitem)
    {
        return swapitem != null && (swapitem.mnItemID == ItemEntries.CargoLiftBasic || swapitem.mnItemID == ItemEntries.CargoLiftImproved || swapitem.mnItemID == ItemEntries.CargoLiftBulk);
    }

    private bool IsLiftUpgrade(ItemBase oldLift, ItemBase newLift)
    {
        return newLift.mnItemID > oldLift.mnItemID;
    }

    public bool CanDeployLift()
    {
        if (this.mLiftMob != null)
            return this.mLiftMob.mnUsedStorage <= 0;
        return this.mnRailDepth >= 6L;
    }

    public bool SwapLift(ItemBase item)
    {
        if (item != null && !this.IsValidLift(item) && !this.CanDeployLift())
            return false;
        if (this.mLiftItem == null)
        {
            if (item == null)
                return false;
            this.mLiftItem = item;
            ItemManager.SetItemCount(this.mLiftItem, 1);
            this.mLiftItem.SetAmount(1);
            this.QueueDeployLift();
            return true;
        }
        if (item == null)
        {
            this.RetrieveLift();
            return true;
        }
        if (!this.IsLiftUpgrade(this.mLiftItem, item))
            return false;
        this.mLiftItem = item;
        this.UpgradeLift();
        return true;
    }

    public ItemBase GetLift()
    {
        if (this.mLiftMob != null && this.mLiftMob.mnUsedStorage > 0)
            return (ItemBase)null;
        return this.mLiftItem;
    }

    private void SetNewOperatingState(StockpileLiftController.OperatingState lState)
    {
        this.mOperatingState = lState;
        this.mRailCheckTimer = 600f;
    }

    private void QueueDeployLift()
    {
        this.mbDeployLift = true;
        if (this.mOperatingState != StockpileLiftController.OperatingState.InstallingRails)
            return;
        this.SetNewOperatingState(StockpileLiftController.OperatingState.Operating);
    }

    private bool DeployLift()
    {
        if (this.mLiftMob != null)
            return true;
        if (!WorldScript.mbIsServer)
            return this.DeployClientLift();
        long x = this.mnX;
        long z = this.mnZ;
        long num = this.mnY - 2L;
        if (this.mSpawnData != null)
        {
            x = this.mSpawnData.x;
            num = this.mSpawnData.y;
            z = this.mSpawnData.z;
        }
        long y = num >> 4 << 4;
        Segment segment = this.AttemptGetSegment(x, num, z);
        if (segment == null)
            return false;
        this.mLiftMob = MobManager.instance.SpawnMob(MobType.Mod, "steveman0.StockpileLift", segment, x, y, z, WorldHelper.DefaultBlockOffset, Vector3.forward) as StockpileLiftMob;
        this.LiftNeedsLinked = true;
        if (this.mSpawnData == null)
        {
            this.mLiftMob.SetLiftType(StockpileLiftController.GetTypeFromItem(this.mLiftItem));
            this.mLiftMob.Spawn(num);
            this.MarkDirtyDelayed();
        }
        else
        {
            this.mLiftMob.SetData(this.mSpawnData);
            this.mSpawnData = (StockpileLiftMob.StockpileLiftData)null;
        }
        this.mLiftMob.SetController(this);
        this.RequestImmediateNetworkUpdate();
        return true;
    }

    private bool DeployClientLift()
    {
        if (this.mSpawnData == null)
            return true;
        if (!WorldScript.instance.mPlayerFrustrum.ContainsCoords(this.mSpawnData.x, this.mSpawnData.y, this.mSpawnData.z))
            return false;
        StockpileLiftMob cargoLiftMob = MobManager.instance.GetMobByID(this.mSpawnData.serverID) as StockpileLiftMob;
        if (cargoLiftMob == null)
            return false;
        cargoLiftMob.SetData(this.mSpawnData);
        this.mSpawnData = (StockpileLiftMob.StockpileLiftData)null;
        this.mLiftMob = cargoLiftMob;
        return true;
    }

    public float GetDepth(long y, float renderOffset)
    {
        return (float)((double)(y - this.mnY) + (double)renderOffset + 0.5 - 1.0);
    }

    private void UpgradeLift()
    {
        if (this.mLiftMob != null)
        {
            this.mLiftMob.SetLiftType(StockpileLiftController.GetTypeFromItem(this.mLiftItem));
            this.mLiftMob.DropGameObject();
            this.mLiftMob.SpawnGameObject();
            this.MarkDirtyDelayed();
            this.RequestImmediateNetworkUpdate();
        }
        else
            this.mbDeployLift = true;
    }

    private void RetrieveLift()
    {
        if (this.mLiftMob != null)
        {
            StockpileLiftMob cargoLiftMob = this.mLiftMob;
            this.mLiftMob = (StockpileLiftMob)null;
            Debug.LogWarning("Retrieving lift");
            MobManager.instance.DestroyMob((MobEntity)cargoLiftMob);
            this.MarkDirtyDelayed();
            this.RequestImmediateNetworkUpdate();
        }
        this.mLiftItem = (ItemBase)null;
        this.SetNewOperatingState(StockpileLiftController.OperatingState.InstallingRails);
    }

    public void ToggleLoadMode(string payload)
    {
        if (payload == "upper")
        {
            int num = (int)this.mUpperLoadOrder;
            this.mUpperLoadOrder = num % 2 != 0 ? (StockpileLiftMob.CargoLoadOrder)(num - 1) : (StockpileLiftMob.CargoLoadOrder)(num + 1);
        }
        else
        {
            int num = (int)this.mLowerLoadOrder;
            this.mLowerLoadOrder = num % 2 != 0 ? (StockpileLiftMob.CargoLoadOrder)(num - 1) : (StockpileLiftMob.CargoLoadOrder)(num + 1);
        }
        if (this.mLiftMob == null)
            return;
        this.mLiftMob.UpdateOrder();
    }

    public void ToggleLoadOrder()
    {
        StockpileLiftMob.CargoLoadOrder cargoLoadOrder = this.mUpperLoadOrder;
        this.mUpperLoadOrder = this.mLowerLoadOrder;
        this.mLowerLoadOrder = cargoLoadOrder;
        if (this.mLiftMob == null)
            return;
        this.mLiftMob.UpdateOrder();
    }

    public void ResetRailCheck()
    {
        if (this.mOperatingState == StockpileLiftController.OperatingState.InstallingRails)
        {
            this.mnRailDepth = 0L;
            this.mRailState = StockpileLiftController.RailState.Scanning;
        }
        else
        {
            this.SetNewOperatingState(StockpileLiftController.OperatingState.Checking);
            this.mnRailCheckDepth = 0L;
            this.mRailState = StockpileLiftController.RailState.Scanning;
        }
        this.RequestImmediateNetworkUpdate();
    }

    public StockpileLiftController GetCenter()
    {
        if (this.mbIsCenter)
            return this;
        return this.mLinkedCenter;
    }

    public override bool ShouldSave()
    {
        return true;
    }

    private void WriteCargo(BinaryWriter writer)
    {
        int num1 = this.mLiftMob.mnUsedStorage;
        writer.Write(num1);
        if (num1 <= 0)
            return;
        int num2 = 0;
        long position1 = writer.BaseStream.Position;
        writer.Write(num2);
        for (int index = 0; index < this.mLiftMob.mCargo.Count; ++index)
        {
            ItemBase itemBase = this.mLiftMob.mCargo[index];
            if (itemBase != null)
            {
                ItemFile.SerialiseItem(itemBase, writer);
                ++num2;
            }
        }
        if (num2 <= 0)
            return;
        long position2 = writer.BaseStream.Position;
        writer.BaseStream.Seek(position1, SeekOrigin.Begin);
        writer.Write(num2);
        writer.BaseStream.Seek(position2, SeekOrigin.Begin);
    }

    private void ReadCargo(BinaryReader reader)
    {
        List<ItemBase> list = this.mLiftMob == null ? new List<ItemBase>() : this.mLiftMob.mCargo;
        if (reader.ReadInt32() > 0)
        {
            int num = reader.ReadInt32();
            for (int index = 0; index < num; ++index)
            {
                ItemBase itemBase = ItemFile.DeserialiseItem(reader);
                list.Add(itemBase);
            }
        }
        if (this.mSpawnData == null)
            return;
        this.mSpawnData.cargo = list;
    }

    private void WriteStockLimits(BinaryWriter writer)
    {
        if (this.mLiftMob == null)
        {
            Debug.LogWarning("Tried to write stock limits when mLiftMob was null!");
            return;
        }
        writer.Write(this.mLiftMob.StockLimits.Count);
        for (int n = 0; n < this.mLiftMob.StockLimits.Count; n++)
        {
            ItemBase itemBase = this.mLiftMob.StockLimits[n].Item;
            ItemFile.SerialiseItem(itemBase, writer);
            writer.Write(this.mLiftMob.StockLimits[n].StockLimit);
        }
    }

    private List<StockItem> ReadStockLimits(BinaryReader reader)
    {
        List<StockItem> stock = new List<StockItem>();
        int count = reader.ReadInt32();
        for (int n = 0; n < count; n++)
        {
            ItemBase item = ItemFile.DeserialiseItem(reader);
            int limit = reader.ReadInt32();
            if (item != null)
                stock.Add(new StockItem(item, limit));
        }
        if (this.mSpawnData == null)
            return stock;
        this.mSpawnData.stocklimits = stock;
        return stock;
    }

    public void WriteState(BinaryWriter writer, bool forNetwork)
    {
        writer.Write(this.mbIsCenter);
        writer.Write(this.mLinkX);
        writer.Write(this.mLinkY);
        writer.Write(this.mLinkZ);
        if (!this.mbIsCenter)
            return;
        writer.Write(this.mrCurrentPower);
        writer.Write((int)this.mUpperLoadOrder);
        writer.Write((int)this.mLowerLoadOrder);
        writer.Write(this.mnRailDepth);
        writer.Write(this.mnRailCheckDepth);
        writer.Write(this.mnRailCount);
        StockpileLiftMob cargoLiftMob = this.mLiftMob;
        ItemFile.SerialiseItem(this.mLiftItem, writer);
        writer.Write(cargoLiftMob != null);
        if (cargoLiftMob == null)
            return;
        if (forNetwork)
            writer.Write(cargoLiftMob.mnServerID);
        writer.Write((int)cargoLiftMob.mOperatingState);
        writer.Write((int)cargoLiftMob.mLoadingState);
        writer.Write((int)cargoLiftMob.mDirection);
        writer.Write((int)cargoLiftMob.mCurrentLoadOrder);
        writer.Write(cargoLiftMob.mnX);
        writer.Write(cargoLiftMob.mnY);
        writer.Write(cargoLiftMob.mnZ);
        writer.Write(cargoLiftMob.mRenderOffset);
        writer.Write((int)cargoLiftMob.mType);
        writer.Write(cargoLiftMob.mrSpeed);
        writer.Write(cargoLiftMob.mTargetHeight);
        writer.Write(cargoLiftMob.mnUsedStorage);
        this.WriteStockLimits(writer);
    }

    public static StockpileLiftMob.CargoLiftType GetTypeFromItem(ItemBase item)
    {
        if (item.mnItemID == ItemEntries.CargoLiftBasic)
            return StockpileLiftMob.CargoLiftType.Basic;
        if (item.mnItemID == ItemEntries.CargoLiftImproved)
            return StockpileLiftMob.CargoLiftType.Improved;
        return item.mnItemID == ItemEntries.CargoLiftBulk ? StockpileLiftMob.CargoLiftType.Bulk : StockpileLiftMob.CargoLiftType.Basic;
    }

    public void ReadState(BinaryReader reader, int entityVersion, bool fromNetwork)
    {
        this.mbIsCenter = reader.ReadBoolean();
        this.mLinkX = reader.ReadInt64();
        this.mLinkY = reader.ReadInt64();
        this.mLinkZ = reader.ReadInt64();
        this.mMBMState = MachineEntity.MBMState.ReacquiringLink;
        if (!this.mbIsCenter)
            return;
        this.mrCurrentPower = reader.ReadSingle();
        if ((double)this.mrCurrentPower < 0.0)
            this.mrCurrentPower = 0.0f;
        if ((double)this.mrCurrentPower > (double)this.mrMaxPower)
            this.mrCurrentPower = this.mrMaxPower;
        this.mUpperLoadOrder = (StockpileLiftMob.CargoLoadOrder)reader.ReadInt32();
        this.mLowerLoadOrder = (StockpileLiftMob.CargoLoadOrder)reader.ReadInt32();
        this.mnRailDepth = reader.ReadInt64();
        this.mnRailCheckDepth = reader.ReadInt64();
        if (entityVersion >= 1)
            this.mnRailCount = reader.ReadInt32();
        this.mLiftItem = ItemFile.DeserialiseItem(reader);
        if (reader.ReadBoolean())
        {
            int lnID = -1;
            StockpileLiftMob cargoLiftMob = (StockpileLiftMob)null;
            if (fromNetwork)
            {
                lnID = reader.ReadInt32();
                MobEntity mobById = MobManager.instance.GetMobByID(lnID);
                cargoLiftMob = mobById as StockpileLiftMob;
                if (mobById != null && cargoLiftMob == null)
                {
                    Debug.LogError((object)"Received ID for a non-lift mob as part of the controller serialisation!");
                    return;
                }
            }
            StockpileLiftMob.StockpileLiftData data = new StockpileLiftMob.StockpileLiftData();
            data.serverID = lnID;
            data.operatingState = (StockpileLiftMob.OperatingState)reader.ReadInt32();
            data.loadingState = (StockpileLiftMob.LoadingState)reader.ReadInt32();
            data.direction = (StockpileLiftMob.Direction)reader.ReadInt32();
            data.loadOrder = (StockpileLiftMob.CargoLoadOrder)reader.ReadInt32();
            data.x = reader.ReadInt64();
            data.y = reader.ReadInt64();
            data.z = reader.ReadInt64();
            data.renderOffset = reader.ReadSingle();
            data.type = (StockpileLiftMob.CargoLiftType)reader.ReadInt32();
            data.speed = reader.ReadSingle();
            data.targetHeight = reader.ReadInt64();
            data.usedSpace = reader.ReadInt32();
            data.stocklimits = this.ReadStockLimits(reader);
            if (cargoLiftMob == null)
            {
                Debug.LogWarning("Are we reading state with a null cargolift mob?");
                this.mSpawnData = data;
                this.QueueDeployLift();
            }
            else
            {
                cargoLiftMob.SetData(data);
                cargoLiftMob.SetController(this);
                this.mLiftMob = cargoLiftMob;
                if (cargoLiftMob.mnX != this.mnX || cargoLiftMob.mnZ != this.mnZ)
                    Debug.LogError((object)"Serialisation error: Lift mob has different horizontal position from us? wut?");
            }
        }
        else if (this.mLiftItem != null)
        {
            Debug.LogWarning((object)"StockpileLiftController has a lift item, but no lift data stored!");
            this.QueueDeployLift();
        }
        this.RequestLowFrequencyUpdates();
    }

    public override void Write(BinaryWriter writer)
    {
        this.WriteState(writer, false);
        if (!this.mbIsCenter || this.mLiftMob == null)
            return;
        this.WriteCargo(writer);
        this.WriteStockLimits(writer);
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
        this.ReadState(reader, entityVersion, false);
        if (!this.mbIsCenter || this.mLiftItem == null)
            return;
        this.ReadCargo(reader);
        this.ReadStockLimits(reader);
    }

    public override bool ShouldNetworkUpdate()
    {
        return true;
    }

    public override void WriteNetworkUpdate(BinaryWriter writer)
    {
        this.WriteState(writer, true);
    }

    public override void ReadNetworkUpdate(BinaryReader reader)
    {
        this.ReadState(reader, this.GetVersion(), true);
    }

    public override int GetVersion()
    {
        return 1;
    }

    public float GetRemainingPowerCapacity()
    {
        if (this.mLinkedCenter != null)
            return this.mLinkedCenter.GetRemainingPowerCapacity();
        return this.mrMaxPower - this.mrCurrentPower;
    }

    public float GetMaximumDeliveryRate()
    {
        return 1000f;
    }

    public float GetMaxPower()
    {
        if (this.mLinkedCenter != null)
            return this.mLinkedCenter.GetMaxPower();
        return this.mrMaxPower;
    }

    public bool DeliverPower(float amount)
    {
        if (this.mLinkedCenter != null)
            return this.mLinkedCenter.DeliverPower(amount);
        if ((double)amount > (double)this.GetRemainingPowerCapacity())
            return false;
        this.mrCurrentPower += amount;
        this.MarkDirtyDelayed();
        return true;
    }

    public bool WantsPowerFromEntity(SegmentEntity entity)
    {
        if (this.mLinkedCenter != null)
            return this.mLinkedCenter.WantsPowerFromEntity(entity);
        return true;
    }

    public override SegmentEntity GetInteractableEntity()
    {
        if (this.mLinkedCenter != null)
            return this.mLinkedCenter;
        return this;
    }

    public override string GetPopupText()
    {
        if (this.mLinkedCenter != null)
            return this.mLinkedCenter.GetPopupText();
        //UIUtil.HandleThisMachineWindow(this.mLinkedCenter, MachineWindow);
        string str1 = "Stockpile Lift Controller" + "\nPower State: " + this.mPowerState + "\nRack Rail storage: " + this.mnRailCount;
        if (this.mOperatingState == StockpileLiftController.OperatingState.InstallingRails)
            str1 = str1 + "\nOperating State: " + this.mOperatingState + "\nRail Depth: " + this.mnRailDepth.ToString("N0") + "\nRail State: " + this.mRailState;
        if (this.mOperatingState == StockpileLiftController.OperatingState.Operating && this.mLiftMob != null)
        {
            float depth = this.GetDepth(this.mLiftMob.mnY, this.mLiftMob.mRenderOffset);
            string str2 = str1 + "\nmLiftMob.mRenderOffset: " + this.mLiftMob.mRenderOffset.ToString() + "\nLift Depth: " + depth.ToString("N0") + "\nLift State: " + this.mLiftMob.mOperatingState + "\tOrder: " + this.mLiftMob.mCurrentLoadOrder + "\t (" + this.mLiftMob.mrOperatingStateTimer.ToString("F2") + "s)";
            if (this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Loading || this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Unloading)
                str2 = str2 + "\nInventories: " + (this.mLiftMob.mAttachedHoppers.Count + this.mLiftMob.mAttachedConveyors.Count);
            if (this.mLiftMob.mOperatingState == StockpileLiftMob.OperatingState.Travelling)
                str2 = str2 + "\nMoving " + this.mLiftMob.mDirection + "\tSpeed: " + this.mLiftMob.mrSpeed.ToString("N1") + " m/s";
            str1 = str2 + string.Format("\nCargo: {0:N0} / {1:N0}", this.mLiftMob.mnUsedStorage, this.mLiftMob.mnMaxStorage);
        }
        return str1;
    }

    public override HoloMachineEntity CreateHolobaseEntity(Holobase holobase)
    {
        HolobaseEntityCreationParameters parameters = new HolobaseEntityCreationParameters((SegmentEntity)this);
        if (!this.mbIsCenter)
            return (HoloMachineEntity)null;
        HolobaseVisualisationParameters visualisationParameters1 = parameters.AddVisualisation(holobase.PowerStorage);
        visualisationParameters1.Scale = new Vector3(3f, 1f, 3f);
        visualisationParameters1.Color = new Color(1f, 0.7f, 0.1f);
        HolobaseVisualisationParameters visualisationParameters2 = parameters.AddVisualisation("CargoLift", holobase.mPreviewCube);
        visualisationParameters2.Scale = new Vector3(3f, 3f, 3f);
        visualisationParameters2.Color = new Color(0.1f, 0.7f, 1.1f);
        parameters.RequiresUpdates = true;
        return holobase.CreateHolobaseEntity(parameters);
    }

    public override void HolobaseUpdate(Holobase holobase, HoloMachineEntity holoMachineEntity)
    {
        GameObject gameObject = holoMachineEntity.VisualisationObjects[1];
        if (this.mLiftMob == null)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
            float depth = this.GetDepth(this.mLiftMob.mnY, this.mLiftMob.mRenderOffset);
            Vector3 vector3 = holoMachineEntity.VisualisationObjects[0].transform.localPosition + new Vector3(0.0f, depth + 1f, 0.0f);
            if ((double)Holobase.mrBaseActiveTime < 1.0)
                gameObject.transform.localPosition = vector3;
            gameObject.transform.localPosition += (vector3 - gameObject.transform.localPosition) * Time.deltaTime * 5f;
        }
    }

    public enum PowerState
    {
        Powered,
        OutOfPower,
    }

    public enum OperatingState
    {
        InstallingRails,
        Operating,
        Checking,
    }

    public enum RailState
    {
        WaitingForResources,
        Scanning,
        Building,
        Blocked,
    }
}

