using System;
using Baiye.Resurrection.Rescue;
static class AnimationChecks {
 static void Assert(bool ok,string name){if(!ok)throw new Exception(name);}
 public static void Run(){
  var a=new WatcherAnimation();
  var idle=new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,false,0);
  Assert(a.Update(idle,0).Station=="idle","powered idle");
  var outbound=new WatcherAnimation.Input(WatcherPhase.Outbound,false,false,true,false,0);
  Assert(a.Update(outbound,.2f).Drone=="launch","departure launch");
  Assert(a.Update(outbound,.4f).Drone=="launch","one-shot does not restart or disappear");
  Assert(a.Update(outbound,1.5f).Drone=="working_loop","launch completes");
  var loaded=new WatcherAnimation.Input(WatcherPhase.Returning,true,false,true,false,0);
  Assert(a.Update(loaded,1.6f).Drone=="pickup","pickup cue");
  Assert(a.Update(loaded,1.6f).Drone=="pickup","paused time freezes cue");
  Assert(a.Update(loaded,3).Drone=="loaded","loaded flight");
  var arrived=new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,true,0);
  Assert(a.Update(arrived,3.1f).Station=="docking","arrival docking");
  Assert(a.Update(arrived,4.4f).Station=="transfer_in","transfer after docking");
  Assert(a.Update(new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,true,.2f),5.7f).Station=="treating","paid care animates");
  Assert(a.Update(new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,true,.2f),6f).Station!="treating","unpaid care stops");
  Assert(a.Update(new WatcherAnimation.Input(WatcherPhase.Idle,false,true,false,true,.2f),6.2f).Station=="off","power loss overrides cues");
  var blocked=new WatcherAnimation();
  Assert(blocked.Update(new WatcherAnimation.Input(WatcherPhase.Returning,true,false,true,false,0,true),10).Station=="alarm","blocked flight signals alarm");
  var interrupted=new WatcherAnimation();interrupted.Update(idle,0);interrupted.Update(outbound,.2f);
  Assert(interrupted.Update(new WatcherAnimation.Input(WatcherPhase.Outbound,false,false,false,false,0),.3f).Drone=="working_loop","power loss clears stale launch");
  var rewind=new WatcherAnimation();
  var care0=new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,true,0);
  var care1=new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,true,.2f);
  rewind.Update(care0,99);Assert(rewind.Update(care1,100).Station=="treating","paid before rewind");
  Assert(rewind.Update(care1,10).Station!="treating","rewind clears future paid-care timestamp");
  Assert(rewind.Update(care1,10.1f).Station!="treating","no treatment without new paid advance");
  Assert(rewind.Update(new WatcherAnimation.Input(WatcherPhase.Idle,false,true,true,true,.4f),10.2f).Station=="treating","new paid advance after rewind");
  var motion=new WatcherMotionClock();motion.Begin(100);
  Assert(Math.Abs(motion.Progress(100.1f)-.5f)<.001f,"movement advances in simulation time");
  Assert(motion.Progress(100.1f)==motion.Progress(100.1f),"paused clock freezes motion");
  Assert(motion.Progress(10)==1,"rewind snaps to authoritative target");
  Assert(motion.Progress(10)==1,"rewound paused clock remains snapped");
  motion.Begin(10);Assert(Math.Abs(motion.Progress(10.1f)-.5f)<.001f,"new movement after rewind interpolates normally");
  var reset=new WatcherAnimation();Assert(reset.Update(loaded,50).Drone=="loaded","reload starts from current state");
  var cancelled=new WatcherAnimation.Input(WatcherPhase.ReturningEmpty,false,false,false,false,0);
  Assert(a.Update(cancelled,6.3f).Drone=="working_loop","cancel clears stale cues");
  var handoff=new WatcherAnimation();
  var unloading=new WatcherAnimation.Input(WatcherPhase.Returning,true,true,true,false,0,false,WatcherAction.Unload);
  Assert(handoff.Update(unloading,100).Station=="transfer_in","saved unload restores transfer directly");
  Assert(handoff.Update(unloading,100).Drone=="unload","paused handoff keeps its pose");
  Assert(handoff.Update(new WatcherAnimation.Input(WatcherPhase.Returning,true,true,false,false,0,false,WatcherAction.Unload),100.2f).Drone=="unload","power loss does not break protected return handoff");
  var settled=handoff.Update(arrived,101.2f);
  Assert(settled.Station!="docking"&&settled.Station!="transfer_in","completed real unload does not replay cosmetic docking");
  var pickup=new WatcherAnimation();
  Assert(pickup.Update(new WatcherAnimation.Input(WatcherPhase.Returning,true,false,true,false,0,false,WatcherAction.Pickup),0).Drone=="pickup","saved pickup restores its protected basket pose");
  Assert(pickup.Update(loaded,1.2f).Drone=="loaded","pickup completes into loaded flight without second pickup");
  Console.WriteLine("31 animation assertions passed");
 }
}
