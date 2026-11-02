using HarmonyLib;
using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 [SerializationConfig(MemberSerialization.OptIn)]
 public sealed class WatcherCustody : KMonoBehaviour,ISaveLoadable {
  [Serialize] public Ref<KPrefabID> Owner;
  [Serialize] public bool AddedBreathTag;
  public bool Protected { get { var owner=Owner?.Get(); return owner!=null && (owner.GetComponent<WatcherStation>()?.Owns(gameObject)==true || owner.GetComponent<WatcherFallback>()?.Owns(gameObject)==true); } }
  public void Begin(KMonoBehaviour station) { Owner=new Ref<KPrefabID>(station.GetComponent<KPrefabID>()); if(!gameObject.HasTag(GameTags.Corpse) && !gameObject.HasTag(GameTags.Dead) && !gameObject.HasTag(GameTags.RecoveringBreath)) { gameObject.AddTag(GameTags.RecoveringBreath); AddedBreathTag=true; } }
  public void End() { if(AddedBreathTag) gameObject.RemoveTag(GameTags.RecoveringBreath); AddedBreathTag=false; Owner=null; }
  public static bool IsProtected(GameObject target) => target!=null && target.GetComponent<WatcherCustody>()?.Protected==true;
 }
 [HarmonyPatch(typeof(IncapacitationMonitor.Instance),nameof(IncapacitationMonitor.Instance.Bleed))]
 internal static class WatcherBleedPatch { static bool Prefix(IncapacitationMonitor.Instance __instance) => !WatcherCustody.IsProtected(__instance.gameObject); }
 [HarmonyPatch(typeof(SuffocationMonitor),"UpdateTimeUntilDeath")]
 internal static class WatcherSuffocationPatch { static bool Prefix(SuffocationMonitor.Instance smi) => !WatcherCustody.IsProtected(smi.gameObject); }
 [HarmonyPatch(typeof(Health),nameof(Health.Damage),new[]{typeof(float)})]
 internal static class WatcherDamagePatch { static bool Prefix(Health __instance) => !WatcherCustody.IsProtected(__instance.gameObject); }
 [HarmonyPatch(typeof(DeathMonitor.Instance),nameof(DeathMonitor.Instance.Kill),new[]{typeof(Death)})]
 internal static class WatcherDeathPatch { static bool Prefix(DeathMonitor.Instance __instance) => !WatcherCustody.IsProtected(__instance.gameObject); }
}
