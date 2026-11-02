using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 [SerializationConfig(MemberSerialization.OptIn)]
 public sealed class WatcherVisual : KMonoBehaviour,ISim200ms,IRenderEveryTick {
  GameObject drone,stationFront,droneFront,observedLiving,observedExit,manualRoot;
  KBatchedAnimController droneAnim,stationFrontAnim,droneFrontAnim;
  ChamberPatientVisual bedDisplay,cargoDisplay,exitDisplay,manualSourceDisplay;
  string lastDrone,lastStation;int shownCell=Grid.InvalidCell;WatcherAction observedAction;
  Vector3 from,to;float fromScale,toScale,stationStarted,droneStarted;bool positioned;
  readonly WatcherAnimation policy=new WatcherAnimation();readonly WatcherMotionClock motion=new WatcherMotionClock();
  readonly WatcherValueClock actionClock=new WatcherValueClock(),careClock=new WatcherValueClock(),exitClock=new WatcherValueClock(),entryClock=new WatcherValueClock();
  public void Sim200ms(float dt){
   var station=GetComponent<WatcherStation>();if(drone==null)Create();if(drone==null)return;
   var housing=GetComponent<KBatchedAnimController>();float now=GameClock.Instance.GetTime();
   if(positioned)ApplyPosition(now);
   bool home=station.DroneCell==station.Home;
   if(Grid.IsValidCell(station.DroneCell)&&(station.DroneCell!=shownCell||!positioned)){
    bool found;var dock=housing.GetSymbolTransform("drone_dock_anchor",out found).MultiplyPoint(Vector3.zero);
    var next=home&&found?dock:Grid.CellToPosCCC(station.DroneCell,Grid.SceneLayer.Move);next.z=Grid.GetLayerZ(Grid.SceneLayer.Move);
    float scale=WatcherInteraction.DroneScale*(home?WatcherInteraction.DockedRatio:1);
    from=positioned?drone.transform.GetPosition():next;fromScale=positioned?droneAnim.animScale:scale;
    to=next;toScale=scale;shownCell=station.DroneCell;motion.Begin(now);positioned=true;
   }
   if(observedAction!=station.Action){observedAction=station.Action;actionClock.Reset(station.ActionSeconds,now);}else actionClock.Observe(station.ActionSeconds,now);
   if(observedLiving!=station.LivingObject){observedLiving=station.LivingObject;careClock.Reset(station.CareSeconds,now);entryClock.Reset(station.BedEntryProgress,now);}else{careClock.Observe(station.CareSeconds,now);entryClock.Observe(station.BedEntryProgress,now);}
   if(observedExit!=station.ExitObject){observedExit=station.ExitObject;exitClock.Reset(station.ExitProgress,now);}else exitClock.Observe(station.ExitProgress,now);
   var op=GetComponent<Operational>();
   var state=policy.Update(new WatcherAnimation.Input(station.Phase,station.CargoObject!=null,home,op!=null&&op.IsOperational,station.LivingObject!=null,station.CareSeconds,station.FlightBlocked,station.Action,station.ExitObject!=null),now);
   if(lastStation!=state.Station){lastStation=state.Station;stationStarted=now;housing.Play(state.Station,state.StationOnce?KAnim.PlayMode.Once:KAnim.PlayMode.Loop);stationFrontAnim.Play("fg_"+state.Station,state.StationOnce?KAnim.PlayMode.Once:KAnim.PlayMode.Loop);}
   if(lastDrone!=state.Drone){lastDrone=state.Drone;droneStarted=now;droneAnim.Play(state.Drone,state.DroneOnce?KAnim.PlayMode.Once:KAnim.PlayMode.Loop);droneFrontAnim.Play("fg_"+state.Drone,state.DroneOnce?KAnim.PlayMode.Once:KAnim.PlayMode.Loop);}
  }
  public void RenderEveryTick(float dt){
   if(drone==null||!positioned)return;float now=GameClock.Instance.GetTime();ApplyPosition(now);
   var station=GetComponent<WatcherStation>();var housing=GetComponent<KBatchedAnimController>();
   if(station.ExitObject==null||station.ExitHealing||station.ExitObject.HasTag(GameTags.Corpse))manualSourceDisplay.Clear();
   var pos=housing.PositionIncludingOffset;pos.z=Grid.GetLayerZ(Grid.SceneLayer.Move)-.02f;stationFront.transform.SetPosition(pos);
   stationFrontAnim.animScale=housing.animScale;stationFrontAnim.Rotation=housing.Rotation;stationFrontAnim.FlipX=housing.FlipX;stationFrontAnim.animWidth=housing.animWidth;stationFrontAnim.animHeight=housing.animHeight;stationFrontAnim.SetDirty();
   pos=drone.transform.GetPosition();pos.z=Grid.GetLayerZ(Grid.SceneLayer.Move)-.02f;droneFront.transform.SetPosition(pos);droneFrontAnim.animScale=droneAnim.animScale;droneFrontAnim.SetDirty();
   float action=WatcherInteraction.Clamp(1-actionClock.Value(now)/WatcherInteraction.ActionDuration),departure=exitClock.Value(now);
   if(station.Action!=WatcherAction.None){
    float pose=WatcherInteraction.Clamp(action*12/11);droneAnim.SetPositionPercent(pose);droneFrontAnim.SetPositionPercent(pose);
    if(station.ExitObject==null&&(station.Action==WatcherAction.Unload||station.Action==WatcherAction.Launch)){housing.SetPositionPercent(pose);stationFrontAnim.SetPositionPercent(pose);}
   }else droneFrontAnim.SetPositionPercent(ClipPercent(lastDrone,now-droneStarted));
   if(station.ExitObject!=null){housing.SetPositionPercent(departure*12/11);stationFrontAnim.SetPositionPercent(departure*12/11);}
   else if(station.Action!=WatcherAction.Unload&&station.Action!=WatcherAction.Launch)stationFrontAnim.SetPositionPercent(ClipPercent(lastStation,now-stationStarted));
   var living=station.LivingObject;
   if(living!=null&&!living.HasTag(GameTags.Corpse)&&!living.HasTag(GameTags.Dead)){
    if(living==station.ExitObject)DrawExit(station,bedDisplay,housing,departure);
    else if(station.BedEntryProgress<1||!station.BedEntryEndpointHeld)bedDisplay.ShowAnimated(living,WatcherInteraction.Sample(WatcherBodyStage.Settle,entryClock.Value(now),false),new WatcherPatientPlacement{Registration=WatcherRegistration.Settle,Anchor=Anchor(housing,"patient_anchor"),Width=1.5f,Height=.85f,Progress=entryClock.Value(now),Flip=true});
    else bedDisplay.ShowAnimated(living,WatcherInteraction.Sample(WatcherBodyStage.Bed,careClock.Value(now)/WatcherInteraction.BedLoopDuration,false),new WatcherPatientPlacement{Registration=WatcherRegistration.Medical,Anchor=Anchor(housing,"patient_anchor"),Width=1.5f,Height=.85f,Flip=true});
   }else bedDisplay.Clear();
   if(station.ExitObject!=null&&station.ExitObject!=living)DrawExit(station,exitDisplay,housing,departure);else exitDisplay.Clear();
   DrawCargo(station,housing,action,now);
  }
  void DrawCargo(WatcherStation station,KBatchedAnimController housing,float progress,float now){
   var cargo=station.CargoObject;if(cargo==null){cargoDisplay.Clear();return;}bool corpse=cargo.HasTag(GameTags.Corpse);float ratio=droneAnim.animScale/WatcherInteraction.DroneScale;
   var point=Anchor(droneAnim,"patient_anchor");var stage=WatcherBodyStage.Carry;float phase=now/.8f;
   var place=new WatcherPatientPlacement{Registration=WatcherRegistration.Center,Anchor=point,Width=1.05f*ratio,Height=.5f*ratio,Flip=station.PickupFlip};
   if(station.Action==WatcherAction.Pickup){
    stage=WatcherBodyStage.Pickup;phase=progress;place.Registration=WatcherRegistration.Pickup;place.Progress=progress;place.SourceOrigin=new Vector3(station.PickupX,station.PickupY,0);place.SourceScale=station.PickupScale;
   }else if(station.Action==WatcherAction.Unload){
    stage=WatcherBodyStage.Place;phase=progress;place.ProjectTurn=true;place.FacingX=WatcherInteraction.TransferFacing(progress,station.PickupFlip);
    // The patient transfer is independent of the station's current mechanical
    // clip, so a simultaneous bed departure cannot redirect incoming cargo.
    var dock=Anchor(housing,"drone_dock_anchor");var bed=Anchor(housing,"patient_anchor");
    var start=dock+new Vector3(0,-70*WatcherInteraction.RawUnitsPerSpritePixel*WatcherInteraction.DroneScale*WatcherInteraction.DockedRatio,0);
    var end=corpse?Anchor(housing,"body_transfer_anchor"):bed;
    if(corpse)end=housing.PositionIncludingOffset+new Vector3(.2f,.48f,0);
    var transfer=WatcherInteraction.Transfer(progress,corpse,start.x,start.y,end.x,end.y);
    place.Anchor=new Vector3(transfer.X,transfer.Y,0);place.Width=transfer.Width;place.Height=transfer.Height;
   }else if(station.Action==WatcherAction.ChamberUnload){
    var receiver=station.Destination?.Get()?.GetComponent<KBatchedAnimController>();if(receiver!=null){float t=WatcherInteraction.Ease(progress);place.Anchor=Vector3.Lerp(point,Anchor(receiver,"patient_anchor"),t);place.Width=Mathf.Lerp(place.Width,.5f,t);place.Height=Mathf.Lerp(place.Height,.4f,t);}
   }
   cargoDisplay.ShowAnimated(cargo,WatcherInteraction.Sample(stage,phase,corpse),place);
  }
  void DrawExit(WatcherStation station,ChamberPatientVisual display,KBatchedAnimController housing,float progress){
   var patient=station.ExitObject;bool corpse=patient.HasTag(GameTags.Corpse);var native=patient.GetComponent<KBatchedAnimController>();
   var placement=new WatcherPatientPlacement{Registration=station.ExitHealing?WatcherRegistration.Exit:WatcherRegistration.Emergency,
    Anchor=corpse?housing.PositionIncludingOffset+new Vector3(.2f,.48f,0):Anchor(housing,"patient_anchor"),DestinationOrigin=Grid.CellToPosCBC(station.ExitCell,Grid.SceneLayer.Move)+(native==null?Vector3.zero:native.Offset),
    Width=corpse?.4f:1.5f,Height=corpse?.22f:.85f,Flip=station.ExitHealing,Progress=station.ExitHealing||corpse?progress:WatcherInteraction.EmergencyMotion(progress),DestinationScale=native==null?.005f:native.animScale,SourceFrame=WatcherInteraction.Sample(WatcherBodyStage.Bed,station.CareSeconds/WatcherInteraction.BedLoopDuration,false).Frame};
   var sample=WatcherInteraction.Sample(station.ExitHealing?WatcherBodyStage.Exit:WatcherBodyStage.EmergencyExit,progress,corpse);
   if(station.ExitHealing&&station.ExitProgress<=0&&!station.ExitReturning){sample=WatcherInteraction.Sample(WatcherBodyStage.Bed,careClock.Value(GameClock.Instance.GetTime())/WatcherInteraction.BedLoopDuration,false);placement.Registration=WatcherRegistration.Medical;}
   display.ShowAnimated(patient,sample,placement);
   if(!station.ExitHealing&&!corpse){
    float blend=WatcherInteraction.EmergencyBlend(progress);display.SetAnimatedOpacity(blend);
    if(blend<1){
     manualSourceDisplay.ShowAnimated(patient,WatcherInteraction.Sample(WatcherBodyStage.Bed,station.CareSeconds/WatcherInteraction.BedLoopDuration,false),new WatcherPatientPlacement{Registration=WatcherRegistration.Medical,Anchor=Anchor(housing,"patient_anchor"),Width=1.5f,Height=.85f,Flip=true});manualSourceDisplay.SetAnimatedOpacity(1-blend);
    }else manualSourceDisplay.Clear();
   }
  }
  static Vector3 Anchor(KBatchedAnimController controller,string name){bool found;var point=controller.GetSymbolTransform(name,out found).MultiplyPoint(Vector3.zero);return found?point:controller.transform.GetPosition();}
  static float ClipPercent(string clip,float time){
   if(clip==null)return 0;float duration=clip=="idle"||clip=="away_idle"||clip=="treating"?1.6f:1.2f;
   bool once=clip=="launch"||clip=="docking"||clip=="transfer_in"||clip=="release"||clip=="pickup"||clip=="unload"||clip=="landing";
   return once?Mathf.Clamp01(time/(duration-.1f)):Mathf.Repeat(Mathf.Max(0,time),duration)/duration;
  }
  void ApplyPosition(float now){float progress=motion.Progress(now);drone.transform.SetPosition(Vector3.Lerp(from,to,progress));droneAnim.animScale=Mathf.Lerp(fromScale,toScale,progress);droneAnim.SetDirty();}
  KBatchedAnimController Controller(string name,KAnimFile file,out GameObject go){
   go=new GameObject(name);go.SetActive(false);go.transform.SetParent(transform,false);
   var anim=go.AddComponent<KBatchedAnimController>();anim.AnimFiles=new[]{file};anim.animScale=WatcherInteraction.DroneScale;anim.sceneLayer=Grid.SceneLayer.Move;anim.initialAnim=name.EndsWith("Front")?"fg_idle":"idle";anim.initialMode=KAnim.PlayMode.Loop;anim.isMovable=true;
   go.SetActive(true);foreach(var component in go.GetComponents<KMonoBehaviour>()){component.InitializeComponent();component.Spawn();}return anim;
  }
  void Create(){
   var file=Assets.GetAnim("baiye_watcher_drone_kanim");var stationFile=Assets.GetAnim("baiye_watcher_station_kanim");if(file==null||stationFile==null)return;
   droneAnim=Controller("BaiyeWatcherDroneVisual",file,out drone);stationFrontAnim=Controller("BaiyeWatcherStationFront",stationFile,out stationFront);droneFrontAnim=Controller("BaiyeWatcherDroneFront",file,out droneFront);
   bedDisplay=GetComponent<ChamberPatientVisual>();bedDisplay.PatientTint=Color.white;
   cargoDisplay=drone.AddComponent<ChamberPatientVisual>();cargoDisplay.PatientTint=Color.white;cargoDisplay.InitializeComponent();cargoDisplay.Spawn();
   exitDisplay=stationFront.AddComponent<ChamberPatientVisual>();exitDisplay.PatientTint=Color.white;exitDisplay.InitializeComponent();exitDisplay.Spawn();
   manualRoot=new GameObject("BaiyeWatcherManualPoseBlend");manualRoot.transform.SetParent(transform,false);manualSourceDisplay=manualRoot.AddComponent<ChamberPatientVisual>();manualSourceDisplay.PatientTint=Color.white;manualSourceDisplay.AnimatedDepthOffset=.001f;manualSourceDisplay.InitializeComponent();manualSourceDisplay.Spawn();
  }
  protected override void OnCleanUp(){if(drone!=null)Util.KDestroyGameObject(drone);if(stationFront!=null)Util.KDestroyGameObject(stationFront);if(droneFront!=null)Util.KDestroyGameObject(droneFront);if(manualRoot!=null)Util.KDestroyGameObject(manualRoot);base.OnCleanUp();}
 }
}
