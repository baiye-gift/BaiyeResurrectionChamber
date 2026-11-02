using System;
using System.Collections.Generic;
using System.Linq;
using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {

 [SerializationConfig(MemberSerialization.OptIn)]
 public sealed class WatcherStation : KMonoBehaviour,ISim200ms,ISaveLoadable {
  [Serialize] public Ref<KPrefabID> Target;
  [Serialize] public Ref<KPrefabID> Living;
  [Serialize] public Ref<KPrefabID> Corpse;
  [Serialize] public Ref<KPrefabID> Cargo;
  [Serialize] public Ref<KPrefabID> Destination;
  [Serialize] public Ref<KPrefabID> FallbackSource;
  [Serialize] public int DeliveryCell=Grid.InvalidCell;
  [Serialize] public int DroneCell=Grid.InvalidCell;
  [Serialize] public WatcherPhase Phase;
  [Serialize] public float CareSeconds;
  [Serialize] public WatcherAction Action;
  [Serialize] public float ActionSeconds;
  [Serialize] public bool ActionEndpointHeld;
  [Serialize] public float PickupX,PickupY,PickupScale=.005f;
  [Serialize] public bool PickupFlip;
  [Serialize] public Ref<KPrefabID> ExitPatient;
  [Serialize] public int ExitCell=Grid.InvalidCell;
  [Serialize] public float ExitProgress;
  [Serialize] public bool ExitReturning,ExitHealing;
  [Serialize] public bool ExitEndpointHeld;
  [Serialize] public bool ExitReturnEndpointHeld;
  [Serialize] public float BedEntryProgress=1;
  [Serialize] public bool BedEntryEndpointHeld=true;
  Storage storage; Operational op; int ticket=-1; float retry,handoffRetry,alertRetry,dispatchRetry,pathRetry; string reason=""; int[] route; int routeGoal=Grid.InvalidCell,routeIndex;
  static readonly Dictionary<int,WatcherStation> claims=new Dictionary<int,WatcherStation>();
  static readonly List<WatcherStation> stations=new List<WatcherStation>();
  static readonly Dictionary<int,float> lastAlerts=new Dictionary<int,float>();
  public string Reason=>reason;
  public bool FlightBlocked=>Phase!=WatcherPhase.Idle && route==null && pathRetry>0;
  public static bool IsClaimed(GameObject go) { var id=go?.GetComponent<KPrefabID>();return id!=null && claims.TryGetValue(id.InstanceID,out var owner)&&owner!=null; }
  public GameObject CargoObject=>Cargo?.Get()?.gameObject;
  public GameObject LivingObject=>Living?.Get()?.gameObject;
  public GameObject CorpseObject=>Corpse?.Get()?.gameObject;
  public GameObject ExitObject=>ExitPatient?.Get()?.gameObject;
  public bool Owns(GameObject patient)=>patient!=null && storage!=null && storage.items.Contains(patient) && patient.GetComponent<WatcherCustody>()?.Owner?.Get()==GetComponent<KPrefabID>();
  GameObject TargetObject=>Target?.Get()?.gameObject;
  public int Home {
   get { bool found;var anim=GetComponent<KBatchedAnimController>();
    var point=anim.GetSymbolTransform("drone_dock_anchor",out found).MultiplyPoint(Vector3.zero);
    return found?Grid.PosToCell(point):Grid.CellAbove(Grid.PosToCell(gameObject));
   }
  }
  void BeginAction(WatcherAction action){Action=action;ActionSeconds=WatcherInteraction.ActionDuration;ActionEndpointHeld=false;}
  void CapturePickup(GameObject patient,Vector3? origin=null){
   var native=patient.GetComponent<KBatchedAnimController>();var point=origin??(native==null?patient.transform.GetPosition():native.PositionIncludingOffset);PickupX=point.x;PickupY=point.y;
   PickupScale=native==null?.005f:native.animScale;PickupFlip=native!=null&&native.FlipX;
  }
  protected override void OnSpawn() {
   base.OnSpawn();storage=GetComponent<Storage>();op=GetComponent<Operational>();stations.Add(this);
   if(!Grid.IsValidCellInWorld(DroneCell,this.GetMyWorldId())) DroneCell=Home;
   Reconcile();
   var item=new StatusItem("BAIYE_WATCHER_STATE","{TITLE}","{TIP}","",StatusItem.IconType.Info,NotificationType.Neutral,false,OverlayModes.None.ID,showWorldIcon:false);
   item.resolveStringCallback=(text,data)=>text.Replace("{TITLE}",ChamberStrings.T("守望者：","Watcher: ")+PhaseLabel()+" · "+(TargetObject?.GetProperName()??ChamberStrings.T("无目标","no target"))).Replace("{TIP}",reason+"\n"+ChamberStrings.T("急救位：","Patient: ")+(LivingObject?.GetProperName()??"—")+" · "+ChamberStrings.T("遗体位：","Body: ")+(CorpseObject?.GetProperName()??"—"));
   GetComponent<KSelectable>().AddStatusItem(item,this); Subscribe((int)GameHashes.QueueDestroyObject,OnDestroyed);
  }
  void Reconcile() {
   if(LivingObject!=null && !storage.items.Contains(LivingObject)) Living=null;
   if(CorpseObject!=null && !storage.items.Contains(CorpseObject)) Corpse=null;
   if(CargoObject!=null && !storage.items.Contains(CargoObject)) Cargo=null;
   foreach(var item in storage.items.Where(x=>x!=null && x.GetComponent<MinionIdentity>()!=null)) {
    item.AddOrGet<WatcherCustody>().Begin(this);
    if(item!=LivingObject && item!=CorpseObject && item!=CargoObject) { if(item.HasTag(GameTags.Corpse) && CorpseObject==null) Corpse=Ref(item); else if(!item.HasTag(GameTags.Corpse) && LivingObject==null) Living=Ref(item); else { Cargo=Ref(item);Phase=WatcherPhase.Returning; } }
   }
   if(Phase==WatcherPhase.Outbound && (TargetObject==null || !Reserve(TargetObject))) { Target=null;Phase=WatcherPhase.ReturningEmpty; }
   if(Phase==WatcherPhase.OutboundFallback && (TargetObject==null || !ReserveFallback(TargetObject,FallbackSource?.Get()?.GetComponent<WatcherFallback>()))) { Target=null;FallbackSource=null;Phase=WatcherPhase.ReturningEmpty; }
   if((Phase==WatcherPhase.Returning || Phase==WatcherPhase.DeliveringBody) && CargoObject==null) Phase=WatcherPhase.ReturningEmpty;
   if(Action!=WatcherAction.None && (ActionSeconds<0 || float.IsNaN(ActionSeconds)||float.IsInfinity(ActionSeconds)))ActionSeconds=0;
   if(PickupScale<=0||float.IsNaN(PickupScale)||float.IsInfinity(PickupScale))PickupScale=.005f;
   if(Action==WatcherAction.Unload && (Phase!=WatcherPhase.Returning||CargoObject==null) || Action==WatcherAction.ChamberUnload && Phase!=WatcherPhase.DeliveringBody){Action=WatcherAction.None;ActionSeconds=0;}
   ExitProgress=WatcherInteraction.Clamp(ExitProgress);if(ExitObject==null||!Owns(ExitObject))ClearExit();
   BedEntryProgress=WatcherInteraction.Clamp(BedEntryProgress);
  }
  string PhaseLabel() {
   switch(Phase) { case WatcherPhase.OutboundFallback:return ChamberStrings.T("前往紧急托管舱","Recovering emergency custody");case WatcherPhase.Outbound:return ChamberStrings.T("前往目标","Outbound");case WatcherPhase.Returning:return ChamberStrings.T("载人返航","Returning with cargo");case WatcherPhase.ReturningEmpty:return ChamberStrings.T("返航","Returning home");case WatcherPhase.DeliveringBody:return ChamberStrings.T("运送遗体至重建仓","Delivering body");default:return ChamberStrings.T("待命","Idle"); }
  }
  void CheckEmergencyAlerts() {
   foreach(var identity in Components.MinionIdentities.Items) {
    if(identity==null||identity.GetMyWorldId()!=this.GetMyWorldId())continue;var patient=identity.gameObject;
    if(Classify(patient)!=TargetKind.Living||patient.HasTag(GameTags.Stored)||IsClaimed(patient)||WatcherRescueExclusion.ActiveRescuer(patient))continue;
    int id=identity.GetComponent<KPrefabID>().InstanceID;float now=GameClock.Instance.GetTime();if(lastAlerts.TryGetValue(id,out float last)&&now-last<120)continue;
    bool available=stations.Any(st=>st!=null&&st.GetMyWorldId()==this.GetMyWorldId()&&st.op!=null&&st.op.IsOperational&&st.Phase==WatcherPhase.Idle&&st.LivingObject==null&&st.PickupCell(Grid.PosToCell(patient))!=Grid.InvalidCell);
    if(available)continue;lastAlerts[id]=now;
    string title=ChamberStrings.T("失能复制人等待救援：","Incapacitated duplicant needs rescue: ")+patient.GetProperName();
    string tip=ChamberStrings.T("救援站缺电、忙碌、急救位已满或航路受阻。请检查电力、槽位、墙体和门。","Stations are unpowered, busy, full or blocked. Check power, capacity, walls and doors.");
    GetComponent<Notifier>()?.Add(new Notification(title,NotificationType.DuplicantThreatening,(list,data)=>tip,click_focus:patient.transform));
   }
  }
  public bool CanRelease=>ExitObject==null && !(Phase==WatcherPhase.Idle&&LivingObject!=null&&(BedEntryProgress<1||!BedEntryEndpointHeld)) && (Phase==WatcherPhase.Idle && (LivingObject!=null||CorpseObject!=null) || Phase==WatcherPhase.Returning && DroneCell==Home && CargoObject!=null && CargoObject.HasTag(GameTags.Corpse) && CorpseObject!=null) && SafeExit()!=Grid.InvalidCell;
  public void ReleaseSafely() {
   if(!CanRelease)return;var patient=Phase==WatcherPhase.Returning?CorpseObject:LivingObject??CorpseObject;BeginExit(patient,false);
  }
  public void CancelOutbound() { if(Phase==WatcherPhase.Outbound||Phase==WatcherPhase.OutboundFallback){Unreserve();FallbackSource=null;Action=WatcherAction.None;ActionSeconds=0;Phase=WatcherPhase.ReturningEmpty;} }
  static Ref<KPrefabID> Ref(GameObject go)=>new Ref<KPrefabID>(go.GetComponent<KPrefabID>());
  static TargetKind Classify(GameObject go) {
   var id=go?.GetComponent<MinionIdentity>();if(id==null)return TargetKind.None;
   var death=go.GetSMI<DeathMonitor.Instance>();bool pending=death!=null && death.sm.death.Get(death)!=null;
   return RescueRules.Classify(id.model==MinionConfig.MODEL,go.HasTag(GameTags.Incapacitated)||go.GetComponent<Health>()?.IsIncapacitated()==true,go.HasTag(GameTags.SuffocatingIncapacitated),go.HasTag(GameTags.Dying)||go.HasTag(GameTags.Dead),go.HasTag(GameTags.Corpse),pending);
  }
  static TargetKind ClassifyFallback(GameObject go,WatcherFallback fallback) {
   var identity=go?.GetComponent<MinionIdentity>();if(identity==null||fallback==null)return TargetKind.None;
   var death=go.GetSMI<DeathMonitor.Instance>();bool pending=death!=null && death.sm.death.Get(death)!=null;
   return RescueRules.ClassifyEscrow(fallback.Owns(go),identity.model==MinionConfig.MODEL,go.HasTag(GameTags.Dying)||go.HasTag(GameTags.Dead),go.HasTag(GameTags.Corpse),pending);
  }
  bool Reserve(GameObject go) {
   if(go==null || go.HasTag(GameTags.Stored) || ChamberProcess.IsClaimedByChamber(go) || WatcherRescueExclusion.ActiveRescuer(go)) return false;
   var id=go.GetComponent<KPrefabID>();var pick=go.GetComponent<Pickupable>();
   if(id==null || pick==null || pick.FetchTotalAmount<=0 || pick.ReservedAmount>0 || (claims.TryGetValue(id.InstanceID,out var other)&&other!=null&&other!=this))return false;
   claims[id.InstanceID]=this;ticket=pick.Reserve("Watcher",GetComponent<KPrefabID>().InstanceID,pick.FetchTotalAmount); return true;
  }
  bool ReserveFallback(GameObject go,WatcherFallback fallback) {
   if(go==null||fallback==null||fallback.Store==null||!fallback.Store.items.Contains(go))return false;
   var id=go.GetComponent<KPrefabID>();if(id==null||(claims.TryGetValue(id.InstanceID,out var owner)&&owner!=null&&owner!=this))return false;
   claims[id.InstanceID]=this;ticket=-1;return true;
  }
  void Unreserve() {
   var go=TargetObject;if(go!=null) { var id=go.GetComponent<KPrefabID>();if(id!=null&&claims.TryGetValue(id.InstanceID,out var owner)&&owner==this)claims.Remove(id.InstanceID); if(ticket>=0)go.GetComponent<Pickupable>()?.Unreserve("Watcher",ticket); }
   ticket=-1;Target=null;
  }
  bool Passable(int cell) {
   if(!Grid.IsValidCellInWorld(cell,this.GetMyWorldId()) || Grid.Solid[cell]) return false;
   if(Grid.HasDoor[cell]) { var door=Grid.Objects[cell,(int)ObjectLayer.Building]?.GetComponent<Door>();var controller=door==null?null:HarmonyLib.AccessTools.Field(typeof(Door),"controller")?.GetValue(door) as Door.Controller.Instance; if(controller==null||!controller.IsInsideState(controller.sm.open))return false; }
   return true;
  }
  bool FlightPassable(int cell)=>RescueRules.FlightClearance(cell,Grid.WidthInCells,Grid.HeightInCells,Passable);
  int[] PathTo(int goal)=>RescueRules.Path(Grid.WidthInCells,Grid.HeightInCells,DroneCell,goal,FlightPassable,20000);
  int PickupCell(int patientCell,bool requireRoute=true) {
   // A suspended basket needs a hover cell above the body; never lower it into the floor.
   if(!Grid.IsValidCellInWorld(patientCell,this.GetMyWorldId())||!Passable(patientCell))return Grid.InvalidCell;
   int above=Grid.CellAbove(patientCell);
   return FlightPassable(above)&&(!requireRoute||PathTo(above)!=null)?above:Grid.InvalidCell;
  }
  bool StepTo(int goal) {
   if(DroneCell==goal)return true;
   if(route==null&&routeGoal==goal&&pathRetry>0)return false;
   if(route==null||routeGoal!=goal||routeIndex>=route.Length||route[routeIndex]!=DroneCell||routeIndex+1>=route.Length||!FlightPassable(route[routeIndex+1])) { route=PathTo(goal);routeGoal=goal;routeIndex=0; }
   if(route==null) { pathRetry=2;reason=ChamberStrings.T("航路受阻，等待重试","Route blocked; retrying");return false; }
   if(routeIndex+1<route.Length && FlightPassable(route[routeIndex+1]))DroneCell=route[++routeIndex];
   return DroneCell==goal;
  }
  int FindDeliveryCell(ChamberProcess chamber) {
   var building=chamber.GetComponent<Building>();if(building==null||building.PlacementCells==null)return Grid.InvalidCell;
   var footprint=new HashSet<int>(building.PlacementCells);
   foreach(int cell in building.PlacementCells)foreach(var step in new[]{new CellOffset(-1,0),new CellOffset(1,0),new CellOffset(0,-1),new CellOffset(0,1)}) {
    if(!Grid.IsCellOffsetValid(cell,step))continue;int candidate=Grid.OffsetCell(cell,step);
    if(!footprint.Contains(candidate)&&Passable(candidate)&&PathTo(candidate)!=null)return candidate;
   }return Grid.InvalidCell;
  }
  bool FindWorldTarget(TargetKind kind) {
   foreach(var id in Components.MinionIdentities.Items) {
    if(id==null||id.GetMyWorldId()!=this.GetMyWorldId())continue;var go=id.gameObject;
    if(go.HasTag(GameTags.Stored)||Classify(go)!=kind||!RescueRules.CanAccept(kind,LivingObject!=null,CorpseObject!=null)||!Reserve(go))continue;
    Target=Ref(go);if(PickupCell(Grid.PosToCell(go))==Grid.InvalidCell){Unreserve();continue;}Phase=WatcherPhase.Outbound;BeginAction(WatcherAction.Launch);reason="";return true;
   }return false;
  }
  bool FindFallbackTarget(TargetKind kind) {
   foreach(var fallback in WatcherFallback.All.ToArray()) {
    if(fallback==null||fallback.GetMyWorldId()!=this.GetMyWorldId()||fallback.Store==null||PickupCell(Grid.PosToCell(fallback.gameObject))==Grid.InvalidCell)continue;
    foreach(var go in fallback.Store.items.ToArray()) {
     if(go==null||ClassifyFallback(go,fallback)!=kind||!RescueRules.CanAccept(kind,LivingObject!=null,CorpseObject!=null)||!ReserveFallback(go,fallback))continue;
     Target=Ref(go);FallbackSource=Ref(fallback.gameObject);Phase=WatcherPhase.OutboundFallback;BeginAction(WatcherAction.Launch);reason="";return true;
    }
   }return false;
  }
  void FindTarget() {
   if(FindWorldTarget(TargetKind.Living)||FindFallbackTarget(TargetKind.Living)||FindWorldTarget(TargetKind.Corpse)||FindFallbackTarget(TargetKind.Corpse))return;
   reason=ChamberStrings.T("等待可达、未被占用的失能者或遗体","Waiting for an accessible, unclaimed patient or body");
  }
  public void Sim200ms(float dt) {
   if(storage==null)return;pathRetry-=dt;alertRetry-=dt;if(alertRetry<=0){alertRetry=10;CheckEmergencyAlerts();}var d=GetComponent<Deconstructable>();bool occupied=storage.items.Count>0||Phase!=WatcherPhase.Idle;if(d!=null&&d.allowDeconstruction==occupied)d.SetAllowDeconstruction(!occupied);
   op.SetActive(op.IsOperational && (Phase!=WatcherPhase.Idle||LivingObject!=null));
   if(LivingObject!=null&&BedEntryProgress<1)BedEntryProgress=Math.Min(1,BedEntryProgress+dt/WatcherInteraction.ActionDuration);else if(LivingObject!=null&&!BedEntryEndpointHeld)BedEntryEndpointHeld=true;
   AdvanceExit(dt);if(LivingObject!=null) Care(); handoffRetry-=dt; dispatchRetry-=dt;
   if(Action!=WatcherAction.None && ActionSeconds>0){ActionSeconds=Math.Max(0,ActionSeconds-dt);if(ActionSeconds<.001f)ActionSeconds=0;return;}
   if(Action!=WatcherAction.None&&!ActionEndpointHeld){ActionEndpointHeld=true;return;}
   if(Action==WatcherAction.Launch || Action==WatcherAction.Pickup)Action=WatcherAction.None;
   retry-=dt;if(retry>0)return;retry=.2f;
   if(Phase==WatcherPhase.Idle) { if(op.IsOperational && CargoObject==null && dispatchRetry<=0){dispatchRetry=2;FindTarget();} if(Phase==WatcherPhase.Idle && CorpseObject!=null && handoffRetry<=0){handoffRetry=5;Handoff();} return; }
   if(Phase==WatcherPhase.ReturningEmpty) { if(StepTo(Home))Phase=WatcherPhase.Idle;return; }
   if(Phase==WatcherPhase.DeliveringBody) {
    var chamber=Destination?.Get()?.GetComponent<ChamberProcess>();var body=CargoObject;
    if(chamber==null||body==null||!body.HasTag(GameTags.Corpse)||!chamber.RescueInbound||chamber.Target!=body) { if(chamber!=null&&body!=null)chamber.CancelRescueReservation(body);Destination=null;Action=WatcherAction.None;Phase=body==null?WatcherPhase.ReturningEmpty:WatcherPhase.Returning;return; }
    if(!StepTo(DeliveryCell))return;
    if(Action!=WatcherAction.ChamberUnload){BeginAction(WatcherAction.ChamberUnload);return;}
    if(RescueRules.ReturnEmptyAfterCargoDelivery(chamber.TryAcceptReservedRescueBody(body,storage))){body.GetComponent<WatcherCustody>()?.End();Cargo=null;Destination=null;Action=WatcherAction.None;Phase=WatcherPhase.ReturningEmpty;}return;
   }
   if(Phase==WatcherPhase.OutboundFallback) {
    var fallback=FallbackSource?.Get()?.GetComponent<WatcherFallback>();var target=TargetObject;var kind=ClassifyFallback(target,fallback);
    if(!op.IsOperational||fallback==null||target==null||fallback.Store==null||!fallback.Store.items.Contains(target)||!RescueRules.CanAccept(kind,LivingObject!=null,CorpseObject!=null)){Unreserve();FallbackSource=null;Phase=WatcherPhase.ReturningEmpty;return;}
    int pickup=PickupCell(Grid.PosToCell(fallback.gameObject),false);if(pickup==Grid.InvalidCell){reason=ChamberStrings.T("吊篮上方空间受阻，等待重试","Basket hover space blocked; retrying");return;}if(!StepTo(pickup))return;
    CapturePickup(target,Grid.CellToPosCBC(Grid.PosToCell(fallback.gameObject),Grid.SceneLayer.Move));fallback.Store.Drop(target,false);storage.Store(target,true,false,false);
    if(!storage.items.Contains(target)){fallback.Store.Store(target,true,false,false);target.AddOrGet<WatcherCustody>().Begin(fallback);Unreserve();FallbackSource=null;Phase=WatcherPhase.ReturningEmpty;return;}
    target.AddOrGet<WatcherCustody>().Begin(this);Unreserve();FallbackSource=null;Cargo=Ref(target);Phase=WatcherPhase.Returning;BeginAction(WatcherAction.Pickup);return;
   }
   if(Phase==WatcherPhase.Outbound) {
    var target=TargetObject;var kind=Classify(target);
    if(!op.IsOperational||target==null||target.HasTag(GameTags.Stored)||!RescueRules.CanAccept(kind,LivingObject!=null,CorpseObject!=null)||ChamberProcess.IsClaimedByChamber(target)){Unreserve();Phase=WatcherPhase.ReturningEmpty;return;}
    int pickup=PickupCell(Grid.PosToCell(target),false);if(pickup==Grid.InvalidCell){reason=ChamberStrings.T("吊篮上方空间受阻，等待重试","Basket hover space blocked; retrying");return;}if(!StepTo(pickup))return;
    if(Classify(target)!=kind||target.HasTag(GameTags.Stored)){Unreserve();Phase=WatcherPhase.ReturningEmpty;return;}
    CapturePickup(target);Unreserve(); storage.Store(target,true,false,false); if(storage.items.Contains(target)){target.AddOrGet<WatcherCustody>().Begin(this);Cargo=Ref(target);Phase=WatcherPhase.Returning;BeginAction(WatcherAction.Pickup);}else Phase=WatcherPhase.ReturningEmpty;
   } else if(Phase==WatcherPhase.Returning) {
    if(CargoObject==null){Phase=WatcherPhase.ReturningEmpty;return;}
    if(!StepTo(Home))return; var cargo=CargoObject;
    if(cargo.HasTag(GameTags.Corpse)){
     var decision=RescueRules.DecideCargoAtHome(true,CorpseObject!=null,handoffRetry<=0&&op.IsOperational);
     if(decision!=CargoHomeDecision.Unload){
      if(decision==CargoHomeDecision.DeliverCargo){handoffRetry=5;if(TryBeginDelivery(cargo,false))return;}
      reason=ChamberStrings.T("遗体位已满；等待可达的重建仓，或安全释放站内遗体","Body slot full; await an accessible chamber or safely release the held body");return;
     }
     if(Action!=WatcherAction.Unload){BeginAction(WatcherAction.Unload);return;}Corpse=Cargo;
    }
    else {if(LivingObject!=null){reason=ChamberStrings.T("急救位已满，继续保护机载伤员","Patient slot full; holding cargo");return;}if(Action!=WatcherAction.Unload){BeginAction(WatcherAction.Unload);return;}Living=Cargo;CareSeconds=0;BedEntryProgress=0;BedEntryEndpointHeld=false;}
    Cargo=null;Action=WatcherAction.None;Phase=WatcherPhase.Idle;
   }
  }
  internal void Settle(float watts,bool paid) { if(paid&&watts>=479.9f&&op!=null&&op.IsOperational&&LivingObject!=null&&LivingObject!=ExitObject&&BedEntryProgress>=1&&BedEntryEndpointHeld&&CareSeconds<30)CareSeconds=Math.Min(30,CareSeconds+.2f); }
  void Care() {
   var go=LivingObject;if(go==null||go==ExitObject)return;
   if(go.HasTag(GameTags.Corpse)||go.HasTag(GameTags.Dead)||go.HasTag(GameTags.Dying)){if(CorpseObject==null){Corpse=Living;Living=null;}return;}
   if(!op.IsOperational){reason=ChamberStrings.T("急救等待电力","Stabilization requires power");return;}
   if(CareSeconds<30)return;
   BeginExit(go,true);
  }
  void BeginExit(GameObject patient,bool healing){
   if(patient==null||ExitObject!=null||!Owns(patient))return;int cell=SafeExit();
   if(cell==Grid.InvalidCell){reason=ChamberStrings.T("无安全出口，继续保护伤员","No safe exit; patient remains protected");return;}
   ExitPatient=Ref(patient);ExitCell=cell;ExitProgress=0;ExitReturning=false;ExitHealing=healing;ExitEndpointHeld=ExitReturnEndpointHeld=false;
  }
  void ClearExit(){ExitPatient=null;ExitCell=Grid.InvalidCell;ExitProgress=0;ExitReturning=ExitHealing=ExitEndpointHeld=ExitReturnEndpointHeld=false;}
  void AdvanceExit(float dt){
   var patient=ExitObject;if(patient==null)return;
   if(!Owns(patient)||ExitHealing&&(patient.HasTag(GameTags.Corpse)||patient.HasTag(GameTags.Dead)||patient.HasTag(GameTags.Dying))){ClearExit();return;}
   if(!IsSafeExitCell(ExitCell)&&!ExitReturning){ExitReturning=true;ExitReturnEndpointHeld=false;}
   float duration=ExitHealing?WatcherInteraction.ExitDuration:WatcherInteraction.ActionDuration;
   if(ExitReturning){
    if(ExitProgress>0){ExitProgress=Math.Max(0,ExitProgress-dt/duration);return;}
    if(!ExitReturnEndpointHeld){ExitReturnEndpointHeld=true;return;}
    ClearExit();return;
   }
   if(ExitHealing&&!op.IsOperational)return;
   // Keep the endpoint for one simulation slice before exposing the real body.
   if(ExitProgress<1){ExitProgress=Math.Min(1,ExitProgress+dt/duration);return;}
   if(!ExitEndpointHeld){ExitEndpointHeld=true;return;}
   FinishExit();
  }
  void FinishExit(){
   var go=ExitObject;if(go==null)return;
   if(!WatcherInteraction.CanFinishExit(ExitProgress,IsSafeExitCell(ExitCell),op.IsOperational,ExitHealing)){ExitReturning=true;ExitReturnEndpointHeld=false;return;}
   bool healed=ExitHealing;if(healed){
   var health=go.GetComponent<Health>();if(health!=null){health.hitPoints=RescueRules.HealedHealth(health.hitPoints,health.maxHitPoints);health.OnHealthChanged(0);}
   var breath=Db.Get().Amounts.Breath.Lookup(go);if(breath!=null)breath.SetValue(breath.GetMax());
   go.Trigger((int)GameHashes.IncapacitationRecovery);
   }
   var native=go.GetComponent<KBatchedAnimController>();if(native!=null)native.FlipX=ExitHealing;
   go.GetComponent<WatcherCustody>()?.End();storage.Drop(go,false);go.transform.SetPosition(Grid.CellToPosCBC(ExitCell,Grid.SceneLayer.Move));
   if(go==LivingObject){Living=null;CareSeconds=0;}if(go==CorpseObject)Corpse=null;ClearExit();
   reason=healed?ChamberStrings.T("急救完成；可按需使用普通病床","Stabilized; ordinary medical care may continue"):ChamberStrings.T("已在安全地面释放原对象","Original patient or body released onto safe ground");
  }
  bool IsSafeExitCell(int c){
   int world=this.GetMyWorldId(),h=Grid.CellAbove(c),f=Grid.CellBelow(c);var cells=GetComponent<Building>()?.PlacementCells;
   if(cells==null||cells.Length==0||!Grid.IsValidCellInWorld(c,world)||!Grid.IsValidCellInWorld(h,world)||!Grid.IsValidCellInWorld(f,world))return false;
   int left=cells.Min(cell=>cell%Grid.WidthInCells),right=cells.Max(cell=>cell%Grid.WidthInCells);
   int floor=cells.Min(cell=>cell/Grid.WidthInCells);
   if(c/Grid.WidthInCells!=floor||c%Grid.WidthInCells!=left-1&&c%Grid.WidthInCells!=right+1)return false;
   if(c%Grid.WidthInCells>=left&&c%Grid.WidthInCells<=right||!Passable(c)||!Passable(h)||!Grid.Solid[f])return false;
   if(Grid.Element[c].id!=SimHashes.Oxygen||Grid.Element[h].id!=SimHashes.Oxygen||Grid.Mass[c]<.2f||Grid.Mass[h]<.2f||Grid.Temperature[c]<283.15f||Grid.Temperature[c]>313.15f||Grid.Temperature[h]<283.15f||Grid.Temperature[h]>313.15f||Grid.Temperature[f]>323.15f)return false;
   // Check the actual horizontal body corridor, not a drone route that may
   // detour over walls. Only the immediately adjacent side exits are used.
   for(int x=Math.Min(left,c%Grid.WidthInCells);x<=Math.Max(right,c%Grid.WidthInCells);x++){
    int cell=floor*Grid.WidthInCells+x;if(!Passable(cell)||!Passable(Grid.CellAbove(cell))||!Grid.Solid[Grid.CellBelow(cell)])return false;
   }
   return true;
  }
  int SafeExit() {
   var cells=GetComponent<Building>()?.PlacementCells;if(cells==null||cells.Length==0)return Grid.InvalidCell;
   int floor=cells.Min(c=>c/Grid.WidthInCells),left=cells.Min(c=>c%Grid.WidthInCells),right=cells.Max(c=>c%Grid.WidthInCells);
   foreach(int c in new[]{floor*Grid.WidthInCells+left-1,floor*Grid.WidthInCells+right+1}){
    if(IsSafeExitCell(c))return c;
   }return Grid.InvalidCell;
  }
  void Handoff() {
   var body=CorpseObject;if(body==null||body==ExitObject||!op.IsOperational)return;
   if(TryBeginDelivery(body,true))return;
   reason=ChamberStrings.T("遗体等待可达、可接收的生命重建仓","Body awaiting an accessible reconstruction chamber");
  }
  bool TryBeginDelivery(GameObject body,bool parked) {
   if(body==ExitObject)return false;
   foreach(var chamber in UnityEngine.Object.FindObjectsOfType<ChamberProcess>()) {
    if(chamber.GetMyWorldId()!=this.GetMyWorldId())continue;int cell=FindDeliveryCell(chamber);if(cell==Grid.InvalidCell)continue;
    if(!chamber.TryReserveRescueBody(body))continue;
    if(parked){Cargo=Corpse;Corpse=null;}Destination=Ref(chamber.gameObject);DeliveryCell=cell;Phase=WatcherPhase.DeliveringBody;BeginAction(WatcherAction.Launch);return true;
   }return false;
  }
  void OnDestroyed(object data) { var chamber=Destination?.Get()?.GetComponent<ChamberProcess>();if(chamber!=null&&CargoObject!=null)chamber.CancelRescueReservation(CargoObject);Unreserve(); }
  protected override void OnCleanUp(){Unreserve();stations.Remove(this);if(stations.Count==0){claims.Clear();lastAlerts.Clear();}base.OnCleanUp();}
 }
}
