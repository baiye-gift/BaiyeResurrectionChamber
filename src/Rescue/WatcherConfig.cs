using System.Collections.Generic;
using TUNING;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 public sealed class WatcherConfig : IBuildingConfig {
  public const string ID="BaiyeWatcherRescueStation";
  public override BuildingDef CreateBuildingDef() {
   var d=BuildingTemplates.CreateBuildingDef(ID,4,3,"baiye_watcher_station_kanim",100,60,new[]{800f,200f},new[]{"Steel","Ceramic"},373.15f,BuildLocationRule.OnFloor,BUILDINGS.DECOR.PENALTY.TIER1,NOISE_POLLUTION.NOISY.TIER1);
   d.RequiresPowerInput=true; d.EnergyConsumptionWhenActive=480; d.PowerInputOffset=new CellOffset(0,0); d.Overheatable=false; d.AudioCategory="HollowMetal"; return d;
  }
  public override void ConfigureBuildingTemplate(GameObject go,Tag prefabTag) {
   go.AddOrGet<KBatchedAnimController>().animScale=WatcherInteraction.StationScale;
   go.AddOrGet<Operational>(); var s=go.AddOrGet<Storage>(); s.capacityKg=10000; s.ShouldSaveItems=true; s.showInUI=false; s.allowItemRemoval=false; s.allowUIItemRemoval=false;
   s.SetDefaultStoredItemModifiers(new List<Storage.StoredItemModifier>{Storage.StoredItemModifier.Hide,Storage.StoredItemModifier.Seal,Storage.StoredItemModifier.Insulate,Storage.StoredItemModifier.Preserve});
   go.AddOrGet<WatcherStation>(); go.AddOrGet<WatcherControl>(); go.AddOrGet<WatcherVisual>(); go.AddOrGet<ChamberPatientVisual>(); go.AddOrGet<Notifier>();
  }
  public override void DoPostConfigureComplete(GameObject go) { go.GetComponent<KBatchedAnimController>().animScale=WatcherInteraction.StationScale;go.AddOrGet<KBatchedAnimHeatPostProcessingEffect>(); }
  public override void DoPostConfigurePreview(BuildingDef def,GameObject go){go.GetComponent<KBatchedAnimController>().animScale=WatcherInteraction.StationScale;}
  public override void DoPostConfigureUnderConstruction(GameObject go){go.GetComponent<KBatchedAnimController>().animScale=WatcherInteraction.StationScale;}
  public static void Register() {
   Strings.Add("STRINGS.CREATURES.SPECIES."+WatcherFallbackConfig.ID.ToUpperInvariant()+".NAME",ChamberStrings.T("紧急托管舱","Emergency custody capsule"));
   string key="STRINGS.BUILDINGS.PREFABS."+ID.ToUpperInvariant();
   Strings.Add(key+".NAME",ChamberStrings.T("守望者救援站","Watcher Rescue Station"));
   Strings.Add(key+".DESC",ChamberStrings.T("派出飞行救援机，接回失能复制人或遗体。","Dispatch a flying drone to recover incapacitated duplicants and bodies."));
   Strings.Add(key+".EFFECT",ChamberStrings.T("失能者优先，急救至至少 30% 生命；遗体等待生命重建仓接收。","Living emergencies take priority; stabilize to at least 30% health. Bodies await a reconstruction chamber."));
  }
 }
}
