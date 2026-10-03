using HarmonyLib;
using Klei.AI;
using UnityEngine;
namespace Baiye.Resurrection
{
    public sealed class ChamberMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony) { ChamberStrings.Register(); base.OnLoad(harmony); }
    }
    [HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
    internal static class ChamberRegistration
    {
        static void Postfix()
        {
            ModUtil.AddBuildingToPlanScreen((HashedString)"Medical", ChamberStrings.ID);
            var tech=Db.Get().Techs.Get("MedicineIV");
            if(tech!=null && !tech.unlockedItemIDs.Contains(ChamberStrings.ID))tech.unlockedItemIDs.Add(ChamberStrings.ID);
            AddEffect(ChamberStrings.WeakEffect,1800,-3);
            AddEffect(ChamberStrings.SevereEffect,0,-8);
        }
        static void AddEffect(string id, float duration, float penalty)
        {
            if(Db.Get().effects.Exists(id))return;
            bool severe=id==ChamberStrings.SevereEffect;
            var effect = new Effect(id,ChamberStrings.T(severe?"重建后严重虚弱":"重建后休养",severe?"Incomplete reconstruction recovery":"Reconstruction convalescence"),
                ChamberStrings.T(severe?"紧急出仓留下的康复欠额。返回生命重建仓完成治疗才能解除；等待不能消除。":"完成仓内康复，仍需休养 3 周期。",severe?"Return to a reconstruction chamber to settle recovery debt. Time alone cannot remove it.":"Rest for 3 cycles after completing chamber treatment."),duration,true,true,true);
            effect.Add(new AttributeModifier(Db.Get().Attributes.Athletics.Id,penalty,effect.Name));
            effect.Add(new AttributeModifier(Db.Get().Attributes.Strength.Id,penalty,effect.Name));
            Db.Get().effects.Add(effect);
        }
    }
    [HarmonyPatch(typeof(BaseMinionConfig), nameof(BaseMinionConfig.BaseMinion))]
    internal static class PatientPrefab
    {
        static void Postfix(GameObject __result) { __result.AddOrGet<PatientRecord>(); }
    }
    [HarmonyPatch(typeof(StoredMinionConfig), nameof(StoredMinionConfig.CreatePrefab))]
    internal static class StoredPatientPrefab
    {
        static void Postfix(GameObject __result) { __result.AddOrGet<PatientRecord>(); }
    }
    [HarmonyPatch(typeof(Edible), "OnPrefabInit")]
    internal static class RecoveryFoodPrefab
    {
        static void Postfix(Edible __instance)
        {
            if(__instance.FoodInfo!=null && __instance.GetQuality()>=2)
                __instance.GetComponent<KPrefabID>().AddTag(new Tag(ChamberStrings.FoodTag));
        }
    }
    // SetConnectionStatus pays a 200 ms slice in the native circuit manager.
    // The chamber never advances on IsOperational alone.
    [HarmonyPatch(typeof(EnergyConsumer), nameof(EnergyConsumer.SetConnectionStatus))]
    internal static class ChamberPaidPower
    {
        static void Prefix(EnergyConsumer __instance,out float __state) => __state=__instance.GetComponent<ChamberProcess>()!=null?__instance.WattsUsed:0;
        static void Postfix(EnergyConsumer __instance,CircuitManager.ConnectionStatus connection_status,float __state)
        {
            if(__state>0)__instance.GetComponent<ChamberProcess>()?.Settle(__state,connection_status==CircuitManager.ConnectionStatus.Powered);
        }
    }
}
