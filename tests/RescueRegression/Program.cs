using System;
using Baiye.Resurrection.Rescue;
class Program {
 static int count;
 static void Check(bool value,string name) { count++; if(!value) throw new Exception(name); }
 static void Escrow() {
  Check(RescueRules.ClassifyEscrow(true,true,false,false,false)==TargetKind.Living,"suffocation-only patient remains eligible after breathing tag removed at selection");
  Check(RescueRules.ClassifyEscrow(true,true,false,false,false)==TargetKind.Living,"same stored patient remains eligible at pickup after reload");
  Check(RescueRules.ClassifyEscrow(false,true,false,false,false)==TargetKind.None,"no custody means no escrow classification");
  Check(RescueRules.ClassifyEscrow(true,true,true,false,true)==TargetKind.None,"pending death not treated as living");
  Check(RescueRules.ClassifyEscrow(true,true,true,true,true)==TargetKind.Corpse,"actual corpse stays corpse");
 }
 static void CargoFull() {
  Check(RescueRules.DecideCargoAtHome(true,true,true)==CargoHomeDecision.DeliverCargo,"full body slot dispatches cargo to chamber");
  Check(RescueRules.DecideCargoAtHome(true,true,false)==CargoHomeDecision.Hold,"missing chamber safely retains both");
  Check(RescueRules.DecideCargoAtHome(true,false,true)==CargoHomeDecision.Unload,"vacant body slot accepts cargo");
  Check(RescueRules.ReturnEmptyAfterCargoDelivery(true),"completed delivery returns empty and resumes");
  Check(!RescueRules.ReturnEmptyAfterCargoDelivery(false),"failed delivery retains cargo");
  bool parkedBodyStillHeld=true; var atHome=RescueRules.DecideCargoAtHome(true,parkedBodyStillHeld,true);
  bool delivered=atHome==CargoHomeDecision.DeliverCargo && RescueRules.ReturnEmptyAfterCargoDelivery(true);
  Check(delivered && parkedBodyStillHeld,"cargo delivery returns empty without consuming parked body");
  parkedBodyStillHeld=false; Check(RescueRules.DecideCargoAtHome(true,parkedBodyStillHeld,true)==CargoHomeDecision.Unload,"freed slot permits resumed unloading");
 }
 static void Main(string[] args) {
  if(args.Length==3&&args[0]=="--solve-poses"){
   var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
   var requests=System.Text.Json.JsonSerializer.Deserialize<WatcherPoseRequest[]>(System.IO.File.ReadAllText(args[1]),options);
   var result=new System.Collections.Generic.List<object>();foreach(var r in requests)result.Add(new{r.Id,Pose=WatcherInteraction.Place(r.Input,r.Current,r.Reference,r.ReferenceWidth,r.ReferenceHeight,r.Previous,r.Entry)});
   System.IO.File.WriteAllText(args[2],System.Text.Json.JsonSerializer.Serialize(result,options));return;
  }
  if(args.Length==2&&args[0]=="--export-interactions"){
   var samples=new System.Collections.Generic.List<object>();
   foreach(WatcherBodyStage stage in Enum.GetValues(typeof(WatcherBodyStage)))for(int i=0;i<=120;i++){
    float p=i/120f;var frame=WatcherInteraction.Sample(stage,p,false);var dead=WatcherInteraction.Sample(stage,p,true);
    var transfer=WatcherInteraction.Transfer(p,false,1.33f,.865f,-.4f,1.37f);
    samples.Add(new{stage=stage.ToString(),progress=p,file=frame.File,clip=frame.Clip,frame=frame.Frame,count=frame.Count,medical=frame.Medical,deadClip=dead.Clip,deadFrame=dead.Frame,ease=WatcherInteraction.Ease(p),exitStep=WatcherInteraction.ExitStep(p),facingX=WatcherInteraction.TransferFacing(p,true),emergencyMotion=WatcherInteraction.EmergencyMotion(p),emergencyBlend=WatcherInteraction.EmergencyBlend(p),transferX=transfer.X,transferY=transfer.Y,transferWidth=transfer.Width,transferHeight=transfer.Height});
   }
   System.IO.File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(samples,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return;
  }
  if(args.Length>0 && args[0]=="escrow"){ Escrow(); Console.WriteLine($"{count} escrow checks passed"); return; }
  if(args.Length>0 && args[0]=="cargo"){ CargoFull(); Console.WriteLine($"{count} cargo checks passed"); return; }
  InteractionChecks.Run(); AnimationChecks.Run(); Escrow(); CargoFull();
  Check(RescueRules.Classify(true,true,false,false,false,false)==TargetKind.Living,"incapacitated");
  Check(RescueRules.Classify(true,false,true,false,false,false)==TargetKind.Living,"suffocation");
  Check(RescueRules.Classify(true,true,false,true,false,false)==TargetKind.None,"dying blocks live");
  Check(RescueRules.Classify(true,true,false,true,true,false)==TargetKind.Corpse,"corpse is distinct");
  Check(RescueRules.Classify(false,true,false,false,false,false)==TargetKind.None,"nonstandard model");
  Check(RescueRules.Classify(true,false,false,false,false,false)==TargetKind.None,"ordinary low health excluded");
  Check(RescueRules.Classify(true,true,false,false,false,true)==TargetKind.None,"pending death");
  Check(!RescueRules.CanClaim(true,false,false) && !RescueRules.CanClaim(false,true,false) && !RescueRules.CanClaim(false,false,true),"claim exclusions");
  Check(RescueRules.CanClaim(false,false,false),"unclaimed");
  Check(!RescueRules.CanAccept(TargetKind.Living,true,false),"living capacity");
  Check(RescueRules.CanAccept(TargetKind.Corpse,true,false),"independent corpse capacity");
  var cap=new RescueCapacity(); Check(cap.Load(TargetKind.Corpse) && cap.Unload(),"corpse slot filled"); Check(cap.Load(TargetKind.Living),"living picked up"); cap.CargoDied(); Check(!cap.Unload() && cap.Cargo==TargetKind.Corpse,"death while corpse slot full retains cargo"); cap.Release(TargetKind.Corpse); Check(cap.Unload(),"held corpse unloads after slot clears"); var claims=new RescueClaims(); Check(claims.TryClaim(1,10) && !claims.TryClaim(1,11),"exclusive claim"); claims.Release(1,11); Check(!claims.TryClaim(1,11),"other owner cannot release"); claims.Release(1,10); Check(claims.TryClaim(1,11),"claim released by owner");
  Check(RescueRules.HealedHealth(95,100)==95 && Math.Abs(RescueRules.HealedHealth(10,100)-30)<0.001,"30 percent floor");
  var path=RescueRules.Path(3,2,0,5,c=>c!=1 && c!=4,100);
  Check(path==null,"cannot wrap across row or pass barrier");
  path=RescueRules.Path(3,3,0,8,c=>c!=4,100);
  Check(path!=null && path.Length==5 && path[0]==0 && path[4]==8,"four-neighbor route");
  Check(RescueRules.Path(3,3,0,8,c=>true,2)==null,"bounded search");
  Check(!RescueRules.FlightClearance(1,3,3,c=>true),"basket cannot hang below grid");
  Check(!RescueRules.FlightClearance(4,3,3,c=>c!=1),"solid below drone blocks basket clearance");
  Check(!RescueRules.FlightClearance(4,3,3,c=>c!=4),"closed door at drone blocks clearance");
  Check(RescueRules.FlightClearance(4,3,3,c=>true),"two non-solid cells permit liquid and magma flight");
  Check(RescueRules.Path(3,3,3,5,c=>RescueRules.FlightClearance(c,3,3,p=>p!=1&&p!=7),100)==null,"clear root corridor cannot route suspended basket through floor or ceiling");
  Console.WriteLine($"{count} rescue checks passed");
 }
}
