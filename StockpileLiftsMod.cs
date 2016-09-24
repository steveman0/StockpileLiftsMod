using UnityEngine;
using System.Collections.Generic;

public class StockpileLiftsMod : FortressCraftMod
{
    public override ModRegistrationData Register()
    {
        ModRegistrationData modRegistrationData = new ModRegistrationData();
        modRegistrationData.RegisterEntityHandler("steveman0.StockpileLiftController");
        modRegistrationData.RegisterEntityHandler("steveman0.StockpileLiftControllerBlock");
        modRegistrationData.RegisterEntityHandler("steveman0.StockpileLiftControllerCenter");
        modRegistrationData.RegisterEntityHandler("steveman0.StockLiftPlacement");
        modRegistrationData.RegisterMobHandler("steveman0.StockpileLift");

        UIManager.NetworkCommandFunctions.Add("StockpileLiftControllerWindow", new UIManager.HandleNetworkCommand(StockpileLiftControllerWindow.HandleNetworkCommand));

        Debug.Log("Stockpile Lifts Mod V2 registered");

        return modRegistrationData;
    }
    public override void CheckForCompletedMachine(ModCheckForCompletedMachineParameters parameters)
    {
        StockpileLiftController.CheckForCompletedMachine(parameters.Frustrum, parameters.X, parameters.Y, parameters.Z);
    }

    public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
    {
        ModCreateSegmentEntityResults result = new ModCreateSegmentEntityResults();

        foreach (ModCubeMap cubeMap in ModManager.mModMappings.CubeTypes)
        {
            if (cubeMap.CubeType == parameters.Cube)
            {
                if (cubeMap.Key.Equals("steveman0.StockLiftPlacement") || cubeMap.Key.Equals("steveman0.StockpileLiftController") || cubeMap.Key.Equals("steveman0.StockpileLiftControllerBlock") || cubeMap.Key.Equals("steveman0.StockpileLiftControllerCenter"))
                {
                    result.Entity = new StockpileLiftController(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value, parameters.LoadFromDisk);
                }
            }
        }
        return result;
    }

    public override void CreateMobEntity(ModCreateMobParameters parameters, ModCreateMobResults results)
    {
        if (parameters.MobKey == "steveman0.StockpileLift")
            results.Mob = new StockpileLiftMob(parameters);

        //foreach (ModMobMap mobMap in ModManager.mModMappings.Mobs)
        //{
        //    if (mobMap.Key == parameters.MobKey)
        //    {
        //        if (mobMap.Key.Equals("steveman0.StockpileLift"))
        //        {
        //            results.Mob = new StockpileLiftMob(parameters);
        //        }
        //    }
        //}
        base.CreateMobEntity(parameters, results);
    }
}

