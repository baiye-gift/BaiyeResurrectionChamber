namespace Baiye.Resurrection.Rescue {
 public enum WatcherPhase { Idle, Outbound, Returning, ReturningEmpty, DeliveringBody, OutboundFallback }
 public enum WatcherAction { None, Launch, Pickup, Unload, ChamberUnload }
 // Presentation only. Reconstructs the steady state on load; never controls custody.
 public sealed class WatcherMotionClock {
  float start,last; bool active;
  public void Begin(float now){start=last=now;active=true;}
  public float Progress(float now){
   if(!active)return 1;
   if(now<last){active=false;return 1;}
   last=now;return System.Math.Max(0,System.Math.Min(1,(now-start)/.2f));
  }
 }
 public sealed class WatcherAnimation {
  public struct Input {
   public WatcherPhase Phase; public WatcherAction Action; public bool Cargo,Home,Power,Patient,Blocked,Exiting; public float Care;
   public Input(WatcherPhase phase,bool cargo,bool home,bool power,bool patient,float care,bool blocked=false,WatcherAction action=WatcherAction.None,bool exiting=false){Exiting=exiting;Action=action;Blocked=blocked;Phase=phase;Cargo=cargo;Home=home;Power=power;Patient=patient;Care=care;}
  }
  public struct Output { public string Station,Drone; public bool StationOnce,DroneOnce; }
  Input previous; bool initialized; float stationUntil,droneUntil,careAt=-1000; string stationCue,droneCue; int stationStage;
  public Output Update(Input x,float now){
   if(initialized && now < lastTime){stationCue=droneCue=null;stationUntil=droneUntil=0;stationStage=0;careAt=float.NegativeInfinity;initialized=false;}
   if(initialized){
    if(x.Patient && x.Care>previous.Care)careAt=now;
    if(!x.Power){stationCue=droneCue=null;stationStage=0;}
    if(x.Action!=WatcherAction.None || previous.Action!=WatcherAction.None){stationCue=droneCue=null;stationStage=0;}
    else if(x.Phase!=previous.Phase || x.Cargo!=previous.Cargo){
     // A cancel, failure or power loss must clear stale transient cues.
     stationCue=droneCue=null;stationStage=0;
     if(previous.Phase==WatcherPhase.Idle && x.Phase!=WatcherPhase.Idle && x.Power){stationCue="launch";droneCue="launch";stationUntil=droneUntil=now+1.2f;}
     else if((previous.Phase==WatcherPhase.Outbound||previous.Phase==WatcherPhase.OutboundFallback) && x.Cargo && x.Phase==WatcherPhase.Returning){droneCue="pickup";droneUntil=now+1.2f;}
     else if(previous.Cargo && !x.Cargo && x.Home && x.Phase==WatcherPhase.Idle){stationCue="docking";droneCue="unload";stationUntil=droneUntil=now+1.2f;stationStage=1;}
     else if(x.Home && x.Phase==WatcherPhase.Idle && previous.Phase==WatcherPhase.ReturningEmpty){stationCue="docking";droneCue="landing";stationUntil=droneUntil=now+1.2f;}
     else if(previous.Cargo && !x.Cargo && x.Phase==WatcherPhase.ReturningEmpty){droneCue="unload";droneUntil=now+1.2f;}
    }
    if(previous.Patient && !x.Patient && !previous.Exiting && x.Phase==WatcherPhase.Idle && x.Power && stationCue==null){stationCue="release";stationUntil=now+1.2f;}
   }
   if(stationCue!=null && now>=stationUntil){if(stationStage==1 && x.Patient && x.Power){stationCue="transfer_in";stationUntil=now+1.2f;stationStage=0;}else stationCue=null;}
   if(droneCue!=null && now>=droneUntil)droneCue=null;
   if(!x.Power){stationCue=droneCue=null;stationStage=0;}
   var output=new Output();
   output.Station=stationCue ?? (!x.Power?"off":(x.Blocked||x.Cargo&&x.Home)?"alarm":x.Patient&&now>=careAt&&now-careAt<=.21f?"treating":x.Phase!=WatcherPhase.Idle?"away_idle":"idle");
   output.Drone=droneCue ?? (x.Cargo?"loaded":x.Phase==WatcherPhase.Idle?"idle":"working_loop");
   output.StationOnce=stationCue!=null;output.DroneOnce=droneCue!=null;
   if(x.Action!=WatcherAction.None){
    output.Drone=x.Action==WatcherAction.Launch?"launch":x.Action==WatcherAction.Pickup?"pickup":"unload";output.DroneOnce=true;
    if(x.Action==WatcherAction.Unload){output.Station="transfer_in";output.StationOnce=true;}
    else if(x.Action==WatcherAction.Launch){output.Station="launch";output.StationOnce=true;}
   }
   if(x.Exiting){output.Station="release";output.StationOnce=true;}
   previous=x;initialized=true;lastTime=now;return output;
  }
  float lastTime;
 }
}
