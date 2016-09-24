// Decompiled with JetBrains decompiler
// Type: CargoLift_Unity
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 83CA2D52-EBED-46F0-B815-19BE9C0430B6
// Assembly location: C:\Games\Steam\steamapps\common\FortressCraft\64\FC_64_Data\Managed\Assembly-CSharp.dll

using UnityEngine;

public class StockpileLiftUnity : MonoBehaviour
{
    private const float MAX_AUDIO_DIST = 64f;
    private StockpileLiftMob mob;
    private Transform cachedTransform;
    private long previousX;
    private long previousY;
    private long previousZ;
    private Vector3 previousBlockOffset;
    private float previousRenderOffset;
    private long nextX;
    private long nextY;
    private long nextZ;
    private Vector3 nextBlockOffset;
    private float nextRenderOffset;
    private Vector3 nextPos;
    private Vector3 previousPos;
    private float timespan;
    private float remainingTimespan;
    private RotateConstantlyScript CogLeft;
    private RotateConstantlyScript CogRight;
    private AudioSource mSource;
    private Renderer CargoLiftObjRend;
    private MaterialPropertyBlock mMPB;
    private int mnVisualStorage;
    private StockpileLiftMob.OperatingState mLocalState;
    private int mnUpdates;

    private void Start()
    {
        GameObjectWrapper gameObjectWrapper = this.GetComponent<SpawnableObjectScript>().wrapper;
        if (gameObjectWrapper == null)
            return;
        this.mob = gameObjectWrapper.mPayload as StockpileLiftMob;
        this.mnVisualStorage = -1;
        this.cachedTransform = this.transform;
        this.previousX = this.mob.mnX;
        this.previousY = this.mob.mnY;
        this.previousZ = this.mob.mnZ;
        this.previousBlockOffset = this.mob.mBlockOffset;
        this.previousRenderOffset = this.mob.mRenderOffset;
        Vector3 vector3 = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(this.mob.mnX, this.mob.mnY, this.mob.mnZ) + this.mob.mBlockOffset;
        vector3.y += this.mob.mRenderOffset;
        this.previousPos = vector3;
        this.nextPos = this.previousPos;
        this.CogLeft = Extensions.Search(this.transform, "CargoLiftGear_L").GetComponent<RotateConstantlyScript>();
        this.CogRight = Extensions.Search(this.transform, "CargoLiftGear_R").GetComponent<RotateConstantlyScript>();
        this.mSource = Extensions.Search(this.transform, "AudioLoop").GetComponent<AudioSource>();
        this.CargoLiftObjRend = Extensions.Search(this.transform, "CargoLiftPlaceholder").GetComponent<Renderer>();
        this.mMPB = new MaterialPropertyBlock();
        this.mnUpdates = 0;
    }

    private void UpdateVisualStorage()
    {
        if (this.mnVisualStorage == this.mob.mnUsedStorage)
            return;
        if (this.mnVisualStorage < this.mob.mnUsedStorage)
        {
            this.mnVisualStorage += 4;
            if (this.mnUpdates < 60)
                this.mnVisualStorage *= 2;
            if (this.mnVisualStorage > this.mob.mnUsedStorage)
                this.mnVisualStorage = this.mob.mnUsedStorage;
            if ((double)this.mob.mDistanceToPlayer >= 8.0)
                ;
        }
        if (this.mnVisualStorage > this.mob.mnUsedStorage)
        {
            this.mnVisualStorage -= 4;
            if (this.mnUpdates < 60)
                this.mnVisualStorage /= 2;
            if (this.mnVisualStorage < this.mob.mnUsedStorage)
                this.mnVisualStorage = this.mob.mnUsedStorage;
            if ((double)this.mob.mDistanceToPlayer >= 8.0)
                ;
        }
        float num1 = (float)this.mnVisualStorage / (float)this.mob.mnMaxStorage;
        int num2 = 60;
        int num3 = (int)((double)num1 * (double)num2);
        for (int index = 0; index < num2; ++index)
            Extensions.Search(Extensions.Search(this.transform, "Cargo_Layer_" + (object)(index / 15)).gameObject.transform, "Cargo_" + (object)(index % 15)).gameObject.SetActive(index < num3);
    }

    private void UpdateAudioLoop()
    {
        float num1 = Mathf.Abs(this.mob.mrSpeed / 15f);
        float num2 = Mathf.Abs(this.mob.mrSpeed / 25f) * 2f + 0.5f;
        if ((double)this.mob.mDistanceToPlayer > 64.0 || (double)num2 < 0.550000011920929)
        {
            if (!this.mSource.isPlaying)
                return;
            this.mSource.Stop();
        }
        else if (!this.mSource.isPlaying)
        {
            this.mSource.Play();
            this.mSource.volume = 0.0f;
        }
        else
        {
            this.mSource.volume += (num1 - this.mSource.volume) * Time.deltaTime;
            this.mSource.pitch += (num2 - this.mSource.pitch) * Time.deltaTime;
        }
    }

    private void Update()
    {
        if (this.mob == null)
            return;
        ++this.mnUpdates;
        if (this.mob.mOperatingState != this.mLocalState)
        {
            this.mLocalState = this.mob.mOperatingState;
            if (this.mnUpdates > 5 && (double)this.mob.mDistanceToPlayer < 24.0)
            {
                if (this.mLocalState == StockpileLiftMob.OperatingState.Loading)
                    AudioHUDManager.instance.PlayMachineAudio(AudioHUDManager.instance.CargoLiftStop, 1f, 1f, this.transform.position, 32f);
                if (this.mLocalState == StockpileLiftMob.OperatingState.Unloading)
                    AudioHUDManager.instance.PlayMachineAudio(AudioHUDManager.instance.CargoLiftStop, 1f, 1f, this.transform.position, 32f);
                if (this.mLocalState == StockpileLiftMob.OperatingState.Travelling)
                    AudioHUDManager.instance.PlayMachineAudio(AudioHUDManager.instance.CargoLiftStart, 1f, 1f, this.transform.position, 32f);
                if (this.mLocalState == StockpileLiftMob.OperatingState.Stuck)
                    AudioHUDManager.instance.PlayMachineAudio(AudioHUDManager.instance.CargoLiftStop, 1f, 1f, this.transform.position, 32f);
            }
        }
        if (this.mob == null)
            return;
        if (!this.mob.mbActive)
            Debug.LogError((object)"Unity is attempting to control a deactivated cargo lift");
        float num = 1f;
        if (this.mob.mController != null)
            num = this.mob.mController.mrCurrentPower / this.mob.mController.mrMaxPower;
        Color color = Color.white;
        if (this.mob.mType == StockpileLiftMob.CargoLiftType.Basic)
            color = Color.green;
        if (this.mob.mType == StockpileLiftMob.CargoLiftType.Improved)
            color = new Color(0.75f, 0.1f, 1f);
        if (this.mob.mType == StockpileLiftMob.CargoLiftType.Bulk)
            color = new Color(1f, 0.75f, 0.1f);
        this.mMPB.SetColor("_GlowColor", color);
        this.mMPB.SetFloat("_GlowMult", num * 8f);
        this.CargoLiftObjRend.SetPropertyBlock(this.mMPB);
        this.UpdateAudioLoop();
        this.UpdateVisualStorage();
        this.CogLeft.YRot = this.mob.mrSpeed * -1.5f;
        this.CogRight.YRot = this.mob.mrSpeed * 1.5f;
        if (this.mob.mnY != this.nextY || (double)this.mob.mRenderOffset != (double)this.nextRenderOffset)
        {
            this.previousPos = this.nextPos;
            this.previousX = this.nextX;
            this.previousY = this.nextY;
            this.previousZ = this.nextZ;
            this.previousBlockOffset = this.nextBlockOffset;
            this.previousRenderOffset = this.nextRenderOffset;
            this.nextX = this.mob.mnX;
            this.nextY = this.mob.mnY;
            this.nextZ = this.mob.mnZ;
            this.nextBlockOffset = this.mob.mBlockOffset;
            this.nextRenderOffset = this.mob.mRenderOffset;
            Vector3 vector3 = WorldScript.instance.mPlayerFrustrum.GetCoordsToUnity(this.mob.mnX, this.mob.mnY, this.mob.mnZ) + this.mob.mBlockOffset;
            vector3.y += this.mob.mRenderOffset;
            this.nextPos = vector3;
            this.timespan = this.mob.mPreviousTimeStep;
            this.remainingTimespan = this.timespan;
        }
        this.remainingTimespan -= Time.deltaTime;
        this.cachedTransform.position = Vector3.Lerp(this.nextPos, this.previousPos, Mathf.Clamp01(this.remainingTimespan / this.timespan));
    }
}
