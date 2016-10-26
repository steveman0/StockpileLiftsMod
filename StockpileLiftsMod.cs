using UnityEngine;
using System.Collections.Generic;

public class StockpileLiftsMod : FortressCraftMod
{
    public ushort ControllerType = ModManager.mModMappings.CubesByKey["steveman0.StockpileLiftController"].CubeType;
    public ushort PlacementType = ModManager.mModMappings.CubesByKey["MachinePlacement"].CubeType;
    public ushort PlacementValue = ModManager.mModMappings.CubesByKey["MachinePlacement"].ValuesByKey["steveman0.StockLiftPlacement"].Value;

    public override ModRegistrationData Register()
    {
        ModRegistrationData modRegistrationData = new ModRegistrationData();
        modRegistrationData.RegisterEntityHandler("steveman0.StockpileLiftController");
        modRegistrationData.RegisterEntityHandler("steveman0.StockpileLiftControllerBlock");
        modRegistrationData.RegisterEntityHandler("steveman0.StockpileLiftControllerCenter");
        modRegistrationData.RegisterEntityHandler("steveman0.StockLiftPlacement");
        modRegistrationData.RegisterMobHandler("steveman0.StockpileLift");
        modRegistrationData.RegisterEntityUI("steveman0.StockpileLiftController", new StockpileLiftControllerWindow());

        UIManager.NetworkCommandFunctions.Add("steveman0.StockpileLiftControllerWindow", new UIManager.HandleNetworkCommand(StockpileLiftControllerWindow.HandleNetworkCommand));

        Debug.Log("Stockpile Lifts Mod V3 registered");

        return modRegistrationData;
    }
    public override void CheckForCompletedMachine(ModCheckForCompletedMachineParameters parameters)
    {
        StockpileLiftController.CheckForCompletedMachine(parameters.Frustrum, parameters.X, parameters.Y, parameters.Z);
    }

    public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
    {
        ModCreateSegmentEntityResults result = new ModCreateSegmentEntityResults();

        if (parameters.Cube == ControllerType || (parameters.Cube == PlacementType && parameters.Value == PlacementValue))
        {
            parameters.ObjectType = SpawnableObjectEnum.CargoLiftController;
            result.Entity = new StockpileLiftController(parameters);
        }
        return result;
    }

    public override void CreateMobEntity(ModCreateMobParameters parameters, ModCreateMobResults results)
    {
        if (parameters.MobKey == "steveman0.StockpileLift")
            results.Mob = new StockpileLiftMob(parameters);

        base.CreateMobEntity(parameters, results);
    }
}

