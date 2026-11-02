using HarmonyLib;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 // Native rescue jobs do not reserve Pickupable. Deny newly selected jobs while
 // this station owns the outbound claim; never interrupt an already running job.
 internal static class WatcherRescueExclusion {
  internal static readonly Chore.Precondition Unclaimed=new Chore.Precondition { id="BaiyeWatcherUnclaimed",description="Target reserved by Watcher",fn=(ref Chore.Precondition.Context context,object data)=>!WatcherStation.IsClaimed(data as GameObject) };
  internal static bool ActiveRescuer(GameObject target) {
   foreach(var identity in Components.MinionIdentities.Items) {
    if(identity==null)continue;var driver=identity.GetComponent<ChoreDriver>();if(driver==null)continue;
    foreach(var chore in new[]{driver.GetCurrentChore(),driver.GetSMI<ChoreDriver.StatesInstance>()?.GetNextChore()})
     if(chore!=null && chore.gameObject==target && (chore is RescueIncapacitatedChore || chore is ResuscitateSuffocatedChore))return true;
   }return false;
  }
 }
 [HarmonyPatch(typeof(RescueIncapacitatedChore),MethodType.Constructor,new[]{typeof(IStateMachineTarget),typeof(GameObject)})]
 internal static class WatcherNativeRescuePatch { static void Postfix(RescueIncapacitatedChore __instance,GameObject incapacitatedDuplicant) { __instance.AddPrecondition(WatcherRescueExclusion.Unclaimed,incapacitatedDuplicant); } }
 [HarmonyPatch(typeof(ResuscitateSuffocatedChore),MethodType.Constructor,new[]{typeof(IStateMachineTarget),typeof(GameObject)})]
 internal static class WatcherNativeCprPatch { static void Postfix(ResuscitateSuffocatedChore __instance,GameObject incapacitatedDuplicant) { __instance.AddPrecondition(WatcherRescueExclusion.Unclaimed,incapacitatedDuplicant); } }
}
