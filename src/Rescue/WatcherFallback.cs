using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 // Invisible, saveable emergency escrow used only when a station is destroyed.
 public sealed class WatcherFallbackConfig : IEntityConfig {
  public const string ID="BaiyeWatcherEmergencyCustody";
  public GameObject CreatePrefab() {
   var go=EntityTemplates.CreateEntity(ID,ID);go.AddOrGet<SaveLoadRoot>();go.AddOrGet<KPrefabID>();go.AddOrGet<KSelectable>().IsSelectable=true;var hitbox=go.AddOrGet<BoxCollider2D>();hitbox.size=new Vector2(1.5f,0.8f);
   var anim=go.AddOrGet<KBatchedAnimController>();anim.AnimFiles=new[]{Assets.GetAnim("baiye_watcher_drone_kanim")};anim.initialAnim="off";anim.initialMode=KAnim.PlayMode.Loop;anim.sceneLayer=Grid.SceneLayer.Move;anim.animScale=1;
   var s=go.AddOrGet<Storage>();s.ShouldSaveItems=true;s.capacityKg=10000;s.showInUI=false;s.allowItemRemoval=false;s.allowUIItemRemoval=false;
   s.SetDefaultStoredItemModifiers(new List<Storage.StoredItemModifier>{Storage.StoredItemModifier.Hide,Storage.StoredItemModifier.Seal,Storage.StoredItemModifier.Insulate,Storage.StoredItemModifier.Preserve});
   go.AddOrGet<WatcherFallback>();return go;
  }
  public void OnPrefabInit(GameObject go) {} public void OnSpawn(GameObject go) {}
 }
 [SerializationConfig(MemberSerialization.OptIn)]
 public sealed class WatcherFallback : KMonoBehaviour,ISim200ms,ISaveLoadable {
  internal static readonly List<WatcherFallback> All=new List<WatcherFallback>();
  Storage store;float retry;
  internal Storage Store=>store;
  protected override void OnSpawn(){base.OnSpawn();store=GetComponent<Storage>();All.Add(this);foreach(var p in store.items.Where(p=>p!=null))p.AddOrGet<WatcherCustody>().Begin(this);var item=new StatusItem("BAIYE_WATCHER_EMERGENCY_CUSTODY","{TITLE}","{TIP}","",StatusItem.IconType.Info,NotificationType.Bad,false,OverlayModes.None.ID,showWorldIcon:true);item.resolveStringCallback=(text,data)=>text.Replace("{TITLE}",ChamberStrings.T("紧急托管舱：等待重建救援站","Emergency custody: awaiting replacement station")).Replace("{TIP}",ChamberStrings.T("托管对象：","Held: ")+string.Join(", ",store.items.Where(x=>x!=null).Select(x=>x.GetProperName())));GetComponent<KSelectable>().AddStatusItem(item,this);}
  public bool Owns(GameObject p)=>store!=null&&store.items.Contains(p)&&p.GetComponent<WatcherCustody>()?.Owner?.Get()==GetComponent<KPrefabID>();
  protected override void OnCleanUp(){All.Remove(this);base.OnCleanUp();}
  public void Sim200ms(float dt) {
   retry-=dt;if(retry>0)return;retry=5;

   if(store.items.Count==0)Util.KDestroyGameObject(gameObject);
  }
 }
 [HarmonyPatch(typeof(Storage),"OnQueueDestroyObject")]
 internal static class WatcherStorageDestructionPatch {
  static void Prefix(Storage __instance) {
   var station=__instance.GetComponent<WatcherStation>();if(station==null||__instance.items.Count==0)return;
   var prefab=Assets.GetPrefab(WatcherFallbackConfig.ID);if(prefab==null){Debug.LogError("[Watcher] Missing emergency custody prefab; native storage will drop patients");return;}
   var obj=Util.KInstantiate(prefab,station.transform.GetPosition());obj.SetActive(true);var fallback=obj.GetComponent<WatcherFallback>();var dest=obj.GetComponent<Storage>();
   foreach(var patient in __instance.items.ToArray()) {if(patient==null)continue;__instance.Drop(patient,false);dest.Store(patient,true,false,false);patient.AddOrGet<WatcherCustody>().Begin(fallback);}
  }
 }
}
