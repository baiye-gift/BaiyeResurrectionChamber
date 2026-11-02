using System.Collections.Generic;
using System.Linq;
using Baiye.Resurrection.Rescue;
using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 public struct WatcherPatientPlacement {
  public WatcherRegistration Registration;
  public Vector3 Anchor,SourceOrigin,DestinationOrigin;
  public float Width,Height,Progress,SourceScale,DestinationScale;
  public int SourceFrame;
  public bool Flip,ProjectTurn;public float FacingX;
 }
}
namespace Baiye.Resurrection {
 public sealed partial class ChamberPatientVisual {
  struct FrameGeometry {public Vector2 Center,Size,Head,HeadDirection,HeadSecond;}
  readonly Dictionary<string,FrameGeometry[]> interactionFrames=new Dictionary<string,FrameGeometry[]>();
  bool warnedInteractionContract;
  public float AnimatedDepthOffset;
  public void SetAnimatedOpacity(float opacity){if(anim!=null){var color=PatientTint;color.a*=WatcherInteraction.Clamp(opacity);anim.TintColour=color;anim.SetDirty();}}
  // This path is opt-in for Watcher. The original chamber keeps its original
  // Show/SelectPose behavior, color and transport fitting.
  public void ShowAnimated(GameObject patient,WatcherBodyFrame sample,WatcherPatientPlacement placement){
   if(patient==null){Clear();return;}
   if(display==null||source!=patient){Clear();source=patient;Create(patient,true);}
   if(display==null)return;
   followCarrier=null;followHousing=null;
   var pose=anim.GetAnim(sample.Clip);
   if(pose==null){display.SetActive(false);return;}
   var expected=Assets.GetAnim(sample.File)?.GetData();
   if(expected==null||pose.animFile.hashName!=expected.hashName||pose.numFrames!=sample.Count){
    if(!warnedInteractionContract){Debug.LogWarning("[BaiyeWatcher] Native patient animation contract differs: "+sample.File+"/"+sample.Clip);warnedInteractionContract=true;}
    display.SetActive(false);return;
   }
   if(currentPose!=sample.Clip){currentPose=sample.Clip;anim.Play(sample.Clip,KAnim.PlayMode.Paused);}
   int frame=Mathf.Clamp(sample.Frame,0,pose.numFrames-1);
   anim.SetPositionPercent((frame+.0001f)/Mathf.Max(1,pose.numFrames));
   anim.SetSymbolVisiblity("cast",false);
   var current=Geometry(pose)[frame];var reference=Geometry(anim.GetAnim("working_loop"));
   var input=new WatcherPlacementInput{Registration=placement.Registration,AnchorX=placement.Anchor.x,AnchorY=placement.Anchor.y,SourceX=placement.SourceOrigin.x,SourceY=placement.SourceOrigin.y,DestinationX=placement.DestinationOrigin.x,DestinationY=placement.DestinationOrigin.y,Width=placement.Width,Height=placement.Height,Progress=placement.Progress,SourceScale=placement.SourceScale,DestinationScale=placement.DestinationScale,Facing=placement.ProjectTurn?placement.FacingX:placement.Flip?-1:1};
   WatcherGeometry previous=default,entry=default;
   if(placement.Registration==WatcherRegistration.Settle){previous=GeometryData(Geometry(anim.GetAnim("place"))[33]);entry=GeometryData(Geometry(anim.GetAnim("working_pre"))[24]);}
   else if(placement.Registration==WatcherRegistration.Emergency&&!patient.HasTag(GameTags.Corpse)){previous=GeometryData(reference[Mathf.Clamp(placement.SourceFrame,0,reference.Length-1)]);entry=GeometryData(Geometry(anim.GetAnim("place"))[17]);}
   var solved=WatcherInteraction.Place(input,GeometryData(current),GeometryData(reference[0]),reference.Max(f=>f.Size.x),reference.Max(f=>f.Size.y),previous,entry);
   var position=new Vector3(solved.X,solved.Y,Grid.GetLayerZ(Grid.SceneLayer.Move)-.01f+AnimatedDepthOffset);
   if(solved.Affine){
    const float baseScale=.005f;anim.navMatrix=new Matrix2x3{m00=solved.M00/baseScale,m01=-solved.M01/baseScale,m10=-solved.M10/baseScale,m11=solved.M11/baseScale};
    anim.FlipX=false;anim.Rotation=0;anim.animScale=baseScale;anim.animWidth=anim.animHeight=1;
   }else{anim.navMatrix=Matrix2x3.identity;anim.FlipX=solved.Horizontal<0;anim.Rotation=solved.Rotation;anim.animScale=solved.Scale;anim.animHeight=1;anim.animWidth=Mathf.Abs(solved.Horizontal);}
   anim.TintColour=PatientTint;display.transform.SetPosition(position);display.SetActive(true);anim.SetDirty();
  }
  FrameGeometry[] Geometry(KAnim.Anim pose){
   if(interactionFrames.TryGetValue(pose.name,out var cached))return cached;
   var result=new FrameGeometry[pose.numFrames];
   var group=KAnimBatchManager.Instance().GetBatchGroupData(anim.GetBatchGroupID());
   var overrides=display.GetComponent<SymbolOverrideController>().GetSymbolOverrides;
   for(int frameIndex=0;frameIndex<pose.numFrames;frameIndex++){
    if(!pose.TryGetFrame(anim.GetBatchGroupID(),frameIndex,out var frame))continue;
    float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;Vector2 head=Vector2.zero,headDirection=Vector2.right,headSecond=Vector2.up;
    for(int i=0;i<frame.numElements;i++){
     var element=group.frameElements[frame.firstElementIdx+i];if(element.multAlpha<=0||!anim.GetSymbolVisiblity(element.symbol))continue;
     var symbol=group.GetSymbol(element.symbol);int priority=int.MinValue;
     foreach(var replacement in overrides)if(replacement.targetSymbol==new HashedString(element.symbol.HashValue)&&replacement.sourceSymbol!=null&&replacement.priority>=priority){symbol=replacement.sourceSymbol;priority=replacement.priority;}
     if(symbol==null)continue;int index=Mathf.Min(element.frame,Mathf.Max(0,symbol.numLookupFrames-1));if(symbol.GetFrameIdx(index)<0)continue;
     var box=symbol.GetFrame(index);var matrix=(Matrix4x4)element.transform;
     if(element.symbol==new KAnimHashedString("snapto_headshape")){
      var center=matrix.MultiplyPoint((box.bboxMin+box.bboxMax)*.5f);var horizontal=matrix.MultiplyVector(new Vector3(box.bboxMax.x-box.bboxMin.x,0,0));var vertical=matrix.MultiplyVector(new Vector3(0,box.bboxMax.y-box.bboxMin.y,0));
      head=new Vector2(center.x,-center.y);headDirection=new Vector2(horizontal.x,-horizontal.y);headSecond=new Vector2(vertical.x,-vertical.y);
     }
     foreach(var corner in new[]{box.bboxMin,new Vector2(box.bboxMin.x,box.bboxMax.y),box.bboxMax,new Vector2(box.bboxMax.x,box.bboxMin.y)}){
      var p=matrix.MultiplyPoint(corner);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,-p.y);maxY=Mathf.Max(maxY,-p.y);
     }
    }
    result[frameIndex]=float.IsInfinity(minX)?new FrameGeometry{Size=Vector2.one}:new FrameGeometry{Center=new Vector2((minX+maxX)/2,(minY+maxY)/2),Size=new Vector2(maxX-minX,maxY-minY),Head=head,HeadDirection=headDirection,HeadSecond=headSecond};
   }
   interactionFrames[pose.name]=result;return result;
  }
  static WatcherGeometry GeometryData(FrameGeometry geometry)=>new WatcherGeometry{X=geometry.Center.x,Y=geometry.Center.y,Width=geometry.Size.x,Height=geometry.Size.y,HeadX=geometry.Head.x,HeadY=geometry.Head.y,HeadDX=geometry.HeadDirection.x,HeadDY=geometry.HeadDirection.y,HeadEX=geometry.HeadSecond.x,HeadEY=geometry.HeadSecond.y};
 }
}
