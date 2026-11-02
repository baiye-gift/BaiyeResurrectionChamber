using System;
using Baiye.Resurrection.Rescue;
static class InteractionChecks {
 static int count;
 static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 public static void Run(){
  var first=WatcherInteraction.Sample(WatcherBodyStage.Pickup,0,false);
  var lifted=WatcherInteraction.Sample(WatcherBodyStage.Pickup,1,false);
  Check(first.Clip=="pickup"&&first.Frame==0&&lifted.Frame==14,"pickup must sample articulated native frames through final pose");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Place,1,false).Frame==33,"placement reaches native lying frame");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Bed,0,false).Frame!=WatcherInteraction.Sample(WatcherBodyStage.Bed,.5f,false).Frame,"paid bed treatment must animate the human body");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Exit,0,false).Clip=="working_pst"&&WatcherInteraction.Sample(WatcherBodyStage.Exit,1,false).Frame==38,"discharge reaches native standing pose");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Exit,.5f,true).Clip=="incapacitate_death","corpse cannot play standing recovery");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Pickup,0,true).Frame==WatcherInteraction.Sample(WatcherBodyStage.Pickup,1,true).Frame,"corpse limbs do not move like living patient");
  Check(WatcherInteraction.Sample(WatcherBodyStage.EmergencyExit,1,false).Clip=="place","unhealed emergency release remains incapacitated");
  Check(WatcherInteraction.Sample(WatcherBodyStage.EmergencyExit,0,false).Frame==17,"manual release starts with the lying perspective rather than a reflected carried-head pose");
  Check(WatcherInteraction.EmergencyMotion(.1f)==0&&WatcherInteraction.EmergencyMotion(.2f)==0&&WatcherInteraction.EmergencyMotion(1)==1,"manual body movement waits for the native pose blend");
  Check(WatcherInteraction.EmergencyBlend(0)==0&&Math.Abs(WatcherInteraction.EmergencyBlend(.1f)-.5f)<.001&&WatcherInteraction.EmergencyBlend(.2f)==1,"manual release and return share reversible opacity endpoints");
  var clock=new WatcherValueClock();clock.Observe(1,100);clock.Observe(.8f,100.2f);
  Check(Math.Abs(clock.Value(100.3f)-.9f)<.001f,"render interpolation uses confirmed simulation slices");
  Check(clock.Value(100.3f)==clock.Value(100.3f),"pause freezes body frame");
  Check(Math.Abs(clock.Value(100.8f)-.8f)<.001f,"interpolation cannot mint unpaid treatment time");
  Check(Math.Abs(clock.Value(1)-.8f)<.001f,"clock rewind snaps to saved progress");
  Check(Math.Abs(WatcherInteraction.StationScale*WatcherInteraction.RawUnitsPerSpritePixel*400-4)<.001f&&Math.Abs(WatcherInteraction.DroneScale*WatcherInteraction.RawUnitsPerSpritePixel*180-1.8f)<.001f,"compiled raw KAnim 2x pixel scale matches preview world units");
  Check(WatcherInteraction.TransferFacing(0,true)==-1&&Math.Abs(WatcherInteraction.TransferFacing(.125f,true))<.0001&&WatcherInteraction.TransferFacing(.25f,true)==1,"mirrored cargo turns continuously through an edge profile");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Place,.1f,false).Clip=="carry_loop"&&WatcherInteraction.Sample(WatcherBodyStage.Place,.25f,false).Clip=="place","place begins after body turning interval");
  var endpoint=new WatcherValueClock();endpoint.Reset(1.2f,0);bool lastVisible=false;
  for(int tick=1;tick<=7;tick++){float time=tick*.2f;endpoint.Observe(Math.Max(0,1.2f-time),time);for(int r=0;r<6;r++){float p=1-endpoint.Value(time+r/30f)/1.2f;if(WatcherInteraction.Sample(WatcherBodyStage.Pickup,p,false).Frame==14)lastVisible=true;}}
  Check(lastVisible,"production interpolation and endpoint hold expose pickup final frame before next phase");
  var poseInput=new WatcherPlacementInput{Registration=WatcherRegistration.Pickup,AnchorX=1,AnchorY=2,SourceX=5,SourceY=6,Width=1,Height=1,SourceScale=.005f,Facing=1};
  var box=new WatcherGeometry{X=10,Y=20,Width=100,Height=100};
  var origin=WatcherInteraction.Place(poseInput,box,box,100,100);
  Check(origin.X==5&&origin.Y==6&&origin.Scale==.005f,"pickup first frame stays at original native origin and scale");
  poseInput.Registration=WatcherRegistration.Exit;poseInput.Progress=1;poseInput.DestinationX=-2;poseInput.DestinationY=.01f;poseInput.DestinationScale=.005f;
  var grounded=WatcherInteraction.Place(poseInput,box,box,100,100);
  Check(grounded.X==-2&&Math.Abs(grounded.Y-.01f)<.0001f&&grounded.Scale==.005f,"discharge last frame shares true entity origin and native scale");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Settle,0,false).Frame==24&&WatcherInteraction.Sample(WatcherBodyStage.Settle,1,false).Frame==33,"entry uses articulated last ten native getting-on-bed frames");
  Check(WatcherInteraction.Sample(WatcherBodyStage.Bed,30/WatcherInteraction.BedLoopDuration,false).Frame==0,"thirty paid seconds meet the native get-up start at a loop boundary");
  var previous=new WatcherGeometry{X=10,Y=20,Width=200,Height=100,HeadX=-50,HeadY=60,HeadDX=100,HeadDY=0,HeadEX=0,HeadEY=25};
  var entry=new WatcherGeometry{HeadX=5,HeadY=250,HeadDX=-80,HeadDY=20,HeadEX=20,HeadEY=80};
  poseInput.Registration=WatcherRegistration.Settle;poseInput.Progress=0;poseInput.AnchorX=-.4f;poseInput.AnchorY=1.37f;poseInput.Width=1.5f;poseInput.Height=.85f;
  var registered=WatcherInteraction.Place(poseInput,entry,box,200,100,previous,entry);float before=Math.Min(1.5f/200,.85f/100);
  float targetHeadX=registered.X+registered.M00*entry.HeadX+registered.M01*entry.HeadY,targetHeadY=registered.Y+registered.M10*entry.HeadX+registered.M11*entry.HeadY;
  Check(registered.Affine&&Math.Abs(targetHeadX-(-.4f+(-50-10)*before))<.0001&&Math.Abs(targetHeadY-(1.37f+(60-20)*before))<.0001,"entry head center matches previous lying frame despite different native head perspective");
  Check(Math.Abs(registered.M00*entry.HeadDX+registered.M01*entry.HeadDY-previous.HeadDX*before)<.0001&&Math.Abs(registered.M10*entry.HeadEX+registered.M11*entry.HeadEY-previous.HeadEY*before)<.0001,"entry preserves both horizontal and vertical native head quad, not only uniform scale");
  var reverse=new WatcherValueClock();reverse.Reset(.2f,100);reverse.Observe(0,100.2f);
  Check(reverse.Value(100.3f)>0,"reverse retains its interpolation interval before returning to bed");
  Check(reverse.Value(100.4f)<.0001f&&reverse.Value(100.5f)<.0001f,"reverse endpoint can stay visible through the additional held slice");
  Check(!WatcherInteraction.CanFinishExit(.9f,true,true,true),"no early release before visible final frame");
  Check(!WatcherInteraction.CanFinishExit(1,false,true,true),"blocked exit retains custody");
  Check(!WatcherInteraction.CanFinishExit(1,true,false,true),"paid recovery cannot complete during power loss");
  Check(WatcherInteraction.CanFinishExit(1,true,true,true),"completed safe powered recovery permits final commit");
  Check(WatcherInteraction.CanFinishExit(1,true,false,false),"manual emergency release does not require healing power");
  Console.WriteLine(count+" patient interaction checks passed");
 }
}
