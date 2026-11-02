using System;
namespace Baiye.Resurrection.Rescue {
 public enum WatcherBodyStage { Pickup,Carry,Place,Bed,Exit,EmergencyExit,Settle }
 public struct WatcherBodyFrame {
  public string File,Clip;public int Frame,Count;
  public bool Medical;
  public float Percent=>Count<=1?0:(float)Frame/Count;
 }
 public struct WatcherTransfer {public float X,Y,Width,Height;}
 public enum WatcherRegistration { Center,Pickup,Medical,Exit,Emergency,Settle }
 public struct WatcherGeometry {public float X,Y,Width,Height,HeadX,HeadY,HeadDX,HeadDY,HeadEX,HeadEY;}
 public struct WatcherPlacementInput {
  public WatcherRegistration Registration;
  public float AnchorX,AnchorY,SourceX,SourceY,DestinationX,DestinationY;
  public float Width,Height,Progress,SourceScale,DestinationScale,Facing;
 }
 public struct WatcherSolvedPose {public float X,Y,Scale,Horizontal,Rotation,M00,M01,M10,M11;public bool Affine;}
 public struct WatcherPoseRequest {public string Id;public WatcherPlacementInput Input;public WatcherGeometry Current,Reference,Previous,Entry;public float ReferenceWidth,ReferenceHeight;}
 // Production frame selection is also exported by RescueRegression for the
 // offline renderer. There is no second illustrative animation timeline.
 public static class WatcherInteraction {
  // Kanimal compiles every SCML pixel into two raw KAnim units. Unity's
  // controller then uses .005 world units per raw unit: 100 sprite px/m.
  public const float RawUnitsPerSpritePixel=2,StationScale=.005f,DroneScale=.005f,DockedRatio=.55f;
  // Eight body loops in the thirty paid stabilization seconds meet the native
  // working_pst first frame at the same loop boundary, without unpaid progress.
  public const float ActionDuration=1.2f,ExitDuration=1.8f,BedLoopDuration=3.75f;
  public static float Clamp(float value)=>float.IsNaN(value)?0:Math.Max(0,Math.Min(1,value));
  public static float Ease(float value){value=Clamp(value);return value*value*(3-2*value);}
  public static WatcherBodyFrame Sample(WatcherBodyStage stage,float progress,bool corpse){
   if(corpse)return new WatcherBodyFrame{File="anim_incapacitated_kanim",Clip="incapacitate_death",Count=25,Frame=24};
   string clip;int count;bool medical=false,loop=false;
   switch(stage){
    case WatcherBodyStage.Settle:return new WatcherBodyFrame{File="anim_healing_bed_kanim",Clip="working_pre",Count=34,Frame=Math.Min(33,24+(int)(Clamp(progress)*10)),Medical=true};
    case WatcherBodyStage.Pickup:clip="pickup";count=15;break;
    case WatcherBodyStage.Carry:clip="carry_loop";count=24;loop=true;break;
    case WatcherBodyStage.Place:
     if(progress<.25f){clip="carry_loop";count=24;progress=progress/.25f;break;}
     clip="place";count=34;progress=(progress-.25f)/.75f;break;
    case WatcherBodyStage.EmergencyExit:return new WatcherBodyFrame{File="anim_incapacitated_kanim",Clip="place",Count=34,Frame=Math.Min(33,17+(int)(EmergencyMotion(progress)*17))};
    case WatcherBodyStage.Exit:clip="working_pst";count=39;medical=true;break;
    default:clip="working_loop";count=114;medical=true;loop=true;break;
   }
   float p=loop?float.IsNaN(progress)||float.IsInfinity(progress)?0:Math.Max(0,progress-(float)Math.Floor(progress)):Clamp(progress);
   int frame=Math.Min(count-1,(int)Math.Floor(p*count+.0001f));
   return new WatcherBodyFrame{File=medical?"anim_healing_bed_kanim":"anim_incapacitated_kanim",Clip=clip,Count=count,Frame=frame,Medical=medical};
  }
  public static bool CanFinishExit(float progress,bool safe,bool powered,bool healing)=>progress>=1&&safe&&(!healing||powered);
  public static float ExitStep(float progress)=>Ease((progress-.35f)/.65f);
  public static float EmergencyMotion(float progress)=>Clamp((progress-.2f)/.8f);
  public static float EmergencyBlend(float progress)=>Ease(progress/.2f);
  public static float TransferFacing(float progress,bool flipped)=>!flipped||progress>=.25f?1:-(float)Math.Cos(Math.PI*Clamp(progress/.25f));
  public static WatcherTransfer Transfer(float progress,bool corpse,float startX,float startY,float endX,float endY){
   float t=Ease(progress);return new WatcherTransfer{X=startX+(endX-startX)*t,Y=startY+(endY-startY)*t+.35f*(float)Math.Sin(Math.PI*t),Width=1.05f*DockedRatio+((corpse?.4f:1.5f)-1.05f*DockedRatio)*t,Height=.5f*DockedRatio+((corpse?.22f:.85f)-.5f*DockedRatio)*t};
  }
  public static WatcherSolvedPose Place(WatcherPlacementInput p,WatcherGeometry current,WatcherGeometry reference,float referenceWidth,float referenceHeight,WatcherGeometry previous=default,WatcherGeometry entry=default){
   if(p.Registration==WatcherRegistration.Settle||p.Registration==WatcherRegistration.Emergency&&(previous.HeadDX!=0||previous.HeadDY!=0)){
    float final=Math.Min(p.Width/Math.Max(1,referenceWidth),p.Height/Math.Max(1,referenceHeight));
    bool settling=p.Registration==WatcherRegistration.Settle;
    float before=settling?Math.Min(p.Width/Math.Max(1,previous.Width),p.Height/Math.Max(1,previous.Height)):final;
    float sourceHeadX=settling?p.AnchorX+(previous.HeadX-previous.X)*before:p.AnchorX+(reference.X-previous.HeadX)*before;
    float sourceHeadY=settling?p.AnchorY+(previous.HeadY-previous.Y)*before:p.AnchorY+(previous.HeadY-reference.Y)*before;
    float sourceFacing=settling?1:-1,targetFacing=settling?-1:1;
    float s00=previous.HeadDX*before*sourceFacing,s01=previous.HeadEX*before*sourceFacing,s10=previous.HeadDY*before,s11=previous.HeadEY*before;
    float determinant=entry.HeadDX*entry.HeadEY-entry.HeadEX*entry.HeadDY;
    if(Math.Abs(determinant)<.000001f)determinant=.000001f;
    float m00=(s00*entry.HeadEY-s01*entry.HeadDY)/determinant,m01=(-s00*entry.HeadEX+s01*entry.HeadDX)/determinant;
    float m10=(s10*entry.HeadEY-s11*entry.HeadDY)/determinant,m11=(-s10*entry.HeadEX+s11*entry.HeadDX)/determinant;
    float entryX=sourceHeadX-m00*entry.HeadX-m01*entry.HeadY,entryY=sourceHeadY-m10*entry.HeadX-m11*entry.HeadY;
    float t=Ease(p.Progress);
    float endX=settling?p.AnchorX+reference.X*final:p.DestinationX,endY=settling?p.AnchorY-reference.Y*final:p.DestinationY,endScale=settling?final:p.DestinationScale;
    m00+=(targetFacing*endScale-m00)*t;m01*=1-t;m10*=1-t;m11+=(endScale-m11)*t;
    return new WatcherSolvedPose{X=entryX+(endX-entryX)*t,Y=entryY+(endY-entryY)*t,Scale=(float)Math.Sqrt(Math.Abs(m00*m11-m01*m10)),Horizontal=targetFacing,Affine=true,M00=m00,M01=m01,M10=m10,M11=m11};
   }
   float scale=Math.Min(p.Width/Math.Max(1,current.Width),p.Height/Math.Max(1,current.Height));
   if(p.Registration==WatcherRegistration.Medical||p.Registration==WatcherRegistration.Exit){current=reference;scale=Math.Min(p.Width/Math.Max(1,referenceWidth),p.Height/Math.Max(1,referenceHeight));}
   float horizontal=Math.Max(.001f,Math.Abs(p.Facing))*(p.Facing<0?-1:1);
   float x=p.AnchorX-current.X*horizontal*scale,y=p.AnchorY-current.Y*scale;
   if(p.Registration==WatcherRegistration.Pickup){float t=Ease(p.Progress);x=p.SourceX+(x-p.SourceX)*t;y=p.SourceY+(y-p.SourceY)*t;scale=p.SourceScale+(scale-p.SourceScale)*t;}
   else if(p.Registration==WatcherRegistration.Exit||p.Registration==WatcherRegistration.Emergency){float t=p.Registration==WatcherRegistration.Exit?ExitStep(p.Progress):Ease(p.Progress);x+=(p.DestinationX-x)*t;y+=(p.DestinationY-y)*t;scale+=(p.DestinationScale-scale)*t;}
   return new WatcherSolvedPose{X=x,Y=y,Scale=scale,Horizontal=horizontal};
  }
 }
 // Interpolates between already-confirmed saved simulation values. It never
 // predicts a future paid slice and snaps on clock rollback or load.
 public sealed class WatcherValueClock {
  float from,to,at,last;bool initialized;
  public void Reset(float value,float now){from=to=value;at=last=now;initialized=true;}
  public void Observe(float value,float now){
   if(!initialized||now<last){Reset(value,now);return;}
   if(value!=to){from=Value(now);to=value;at=now;}last=now;
  }
  public float Value(float now){
   if(!initialized)return 0;if(now<last){Reset(to,now);return to;}last=now;
   return from+(to-from)*WatcherInteraction.Clamp((now-at)/.2f);
  }
 }
}
