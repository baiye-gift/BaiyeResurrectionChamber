using System.Collections.Generic;
using System.Linq;
using Database;
using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection
{
    // Unsaved visual-only object: no MinionIdentity, AI, collider or patient ID.
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class ChamberPatientVisual : KMonoBehaviour,IRenderEveryTick
    {
        GameObject display;
        GameObject source;
        KBatchedAnimController anim;
        Vector2 poseCenter;
        float fittedScale;
        Vector2 carryCenter;
        float carryScale,carryRotation;
        string currentPose;
        public const string PodPose="idle_default";
        public const string CarryPose="working_loop";
        GameObject followCarrier;
        KBatchedAnimController followHousing;
        public void Show(GameObject patient,KBatchedAnimController housing,bool breathing,GameObject carrier=null)
        {
            if(patient==null){Clear();return;}
            if(display==null||source!=patient){Clear();source=patient;Create(patient);}
            if(display==null)return;
            bool found;var point=housing.GetSymbolTransform("patient_anchor",out found).MultiplyPoint(Vector3.zero);
            if(!found){display.SetActive(false);return;}
            bool carried=carrier!=null;
            SelectPose(carried);
            if(display==null)return;
            followCarrier=carrier;followHousing=housing;
            if(carried)
            {
                var carrierAnim=carrier.GetComponent<KBatchedAnimController>();
                point=carrierAnim.GetSymbolTransform("snapTo_pivot",out found).MultiplyPoint(Vector3.zero);
                if(!found)point=carrier.transform.GetPosition()+new Vector3(0,.8f,0);
            }
            point.z=Grid.GetLayerZ(Grid.SceneLayer.BuildingUse);
            if(carried)point.z=Grid.GetLayerZ(Grid.SceneLayer.Move)-.01f;
            float breath=breathing&&!carried?1+.006f*Mathf.Sin(GameClock.Instance.GetTime()*2):1;
            float scale=carried?carryScale:fittedScale;var center=carried?carryCenter:poseCenter;
            anim.Rotation=carried?carryRotation:0;
            anim.animScale=scale;anim.animHeight=breath;
            // Pod pose is natively upright; only transport may rotate a pose.
            // Remove its source origin offset to center the full visible body.
            point-=new Vector3(center.x*scale,center.y*scale*breath,0);
            display.transform.SetPosition(point);display.SetActive(true);anim.SetDirty();
        }
        public void RenderEveryTick(float dt)
        {
            if(followCarrier!=null&&source!=null)Show(source,followHousing,false,followCarrier);
            else if(source==null&&display!=null)Clear();
        }
        void Create(GameObject patient)
        {
            // Same rendering components as FullMinionUIPortrait, using world
            // rendering without an entity template or selectable/save data.
            display=new GameObject("BaiyeChamberPatientVisual");display.SetActive(false);
            display.transform.SetParent(transform,false);
            anim=display.AddComponent<KBatchedAnimController>();
            anim.AnimFiles=AnimationFiles();anim.animScale=.0045f;
            anim.sceneLayer=Grid.SceneLayer.BuildingUse;anim.initialAnim="";anim.isMovable=true;
            display.AddComponent<FaceGraph>();
            var accessor=display.AddComponent<Accessorizer>();
            var wearable=display.AddComponent<WearableAccessorizer>();
            SymbolOverrideControllerUtil.AddToPrefab(display).applySymbolOverridesEveryFrame=true;
            display.SetActive(true);
            foreach(var component in display.GetComponents<KMonoBehaviour>()){component.InitializeComponent();component.Spawn();}
            // All setup is synchronous before the next rendered frame.
            BaseMinionConfig.ConfigureSymbols(display);
            ApplyAppearance(patient,accessor,wearable);
            SelectPose(false);
            if(anim==null)return;
            anim.TintColour=new Color32(225,249,250,235);
        }
        void SelectPose(bool carried)
        {
            string pose=carried&&anim.GetAnim(CarryPose)!=null?CarryPose:PodPose;
            if(pose==currentPose)return;
            if(anim.GetAnim(pose)==null){Debug.LogWarning("[BaiyeResurrectionChamber] Patient pose unavailable: "+pose);Clear();return;}
            currentPose=pose;anim.Play(pose,KAnim.PlayMode.Paused);anim.SetPositionPercent(0);
            anim.SetSymbolVisiblity("cast",false);FitPatientPose(anim.GetAnim(pose));
        }
        public static KAnimFile[] AnimationFiles()
        {
            var files=new List<KAnimFile>{Assets.GetAnim("body_comp_default_kanim"),Assets.GetAnim("anim_idles_default_kanim")};
            if(Assets.TryGetAnim("anim_healing_bed_kanim",out var medical))files.Add(medical);
            return files.ToArray();
        }
        void FitPatientPose(KAnim.Anim pose)
        {
            fittedScale=carryScale=.0045f;
            var group=KAnimBatchManager.Instance().GetBatchGroupData(anim.GetBatchGroupID());
            if(!pose.TryGetFrame(anim.GetBatchGroupID(),0,out var frame))return;
            var overrides=display.GetComponent<SymbolOverrideController>().GetSymbolOverrides;
            var points=new List<Vector2>();var feet=new List<Vector2>();Vector2 head=Vector2.zero;bool hasHead=false;
            for(int i=0;i<frame.numElements;i++)
            {
                var e=group.frameElements[frame.firstElementIdx+i];
                if(e.multAlpha<=0||!anim.GetSymbolVisiblity(e.symbol))continue;
                var symbol=group.GetSymbol(e.symbol);int priority=int.MinValue;
                foreach(var replacement in overrides)
                    if(replacement.targetSymbol==new HashedString(e.symbol.HashValue)&&replacement.sourceSymbol!=null&&replacement.priority>=priority){symbol=replacement.sourceSymbol;priority=replacement.priority;}
                if(symbol==null)continue;
                int symbolFrame=Mathf.Min(e.frame,Mathf.Max(0,symbol.numLookupFrames-1));
                if(symbol.GetFrameIdx(symbolFrame)<0)continue;
                var box=symbol.GetFrame(symbolFrame);var matrix=(Matrix4x4)e.transform;
                foreach(var corner in new[]{box.bboxMin,new Vector2(box.bboxMin.x,box.bboxMax.y),box.bboxMax,new Vector2(box.bboxMax.x,box.bboxMin.y)})
                {
                    var p=matrix.MultiplyPoint(corner);points.Add(new Vector2(p.x,-p.y));
                }
                if(e.symbol==new KAnimHashedString("snapto_headshape"))
                {
                    var p=matrix.MultiplyPoint(Vector3.zero);head=new Vector2(p.x,-p.y);hasHead=true;
                }
                if(e.symbol==new KAnimHashedString("foot")){var p=matrix.MultiplyPoint(Vector3.zero);feet.Add(new Vector2(p.x,-p.y));}
            }
            if(points.Count==0)return;
            float minX=points.Min(p=>p.x),maxX=points.Max(p=>p.x),minY=points.Min(p=>p.y),maxY=points.Max(p=>p.y);
            bool horizontal=maxX-minX>maxY-minY;
            if(hasHead&&feet.Count>0)
            {
                var axis=head-new Vector2(feet.Average(p=>p.x),feet.Average(p=>p.y));
                horizontal=Mathf.Abs(axis.x)>Mathf.Abs(axis.y);
            }
            carryRotation=horizontal?0:-90;
            var carryPoints=points.Select(p=>(Vector2)(Quaternion.Euler(0,0,carryRotation)*(Vector3)p)).ToList();
            float cMinX=carryPoints.Min(p=>p.x),cMaxX=carryPoints.Max(p=>p.x),cMinY=carryPoints.Min(p=>p.y),cMaxY=carryPoints.Max(p=>p.y);
            carryCenter=new Vector2((cMinX+cMaxX)/2,(cMinY+cMaxY)/2);
            carryScale=Mathf.Min(1.5f/Mathf.Max(1,cMaxX-cMinX),.85f/Mathf.Max(1,cMaxY-cMinY));
            poseCenter=new Vector2((minX+maxX)/2,(minY+maxY)/2);
            fittedScale=Mathf.Min(1.05f/Mathf.Max(1,maxX-minX),1.85f/Mathf.Max(1,maxY-minY));
        }
        public static void ApplyAppearance(GameObject patient,Accessorizer accessor,WearableAccessorizer wearable)
        {
            var stored=patient.GetComponent<StoredMinionIdentity>();
            var original=patient.GetComponent<Accessorizer>();
            var originalWearable=patient.GetComponent<WearableAccessorizer>();
            accessor.bodyData=stored!=null?stored.bodyData:original.bodyData;
            accessor.SetAccessories(CopyAccessories(stored!=null?stored.accessories:original.GetAccessories()));accessor.ApplyAccessories();
            wearable.RestoreWearables(CopyWearables(stored!=null?stored.wearables:originalWearable?.Wearables),
                CopyClothing(stored!=null?stored.customClothingItems:originalWearable?.GetCustomClothingItems()));
            accessor.UpdateHairBasedOnHat();
            if(stored==null)BaseMinionConfig.CopyVisibleSymbols(accessor.gameObject,patient);
        }
        public static List<ResourceRef<Accessory>> CopyAccessories(List<ResourceRef<Accessory>> input)=>
            input==null?new List<ResourceRef<Accessory>>():new List<ResourceRef<Accessory>>(input.Where(x=>x!=null&&x.Get()!=null));
        public static Dictionary<ClothingOutfitUtility.OutfitType,List<ResourceRef<ClothingItemResource>>> CopyClothing(
            Dictionary<ClothingOutfitUtility.OutfitType,List<ResourceRef<ClothingItemResource>>> input)
        {
            var result=new Dictionary<ClothingOutfitUtility.OutfitType,List<ResourceRef<ClothingItemResource>>>();
            if(input!=null)foreach(var pair in input)
                result[pair.Key]=pair.Value==null?new List<ResourceRef<ClothingItemResource>>():new List<ResourceRef<ClothingItemResource>>(pair.Value.Where(x=>x!=null&&x.Get()!=null));
            return result;
        }
        public static Dictionary<WearableAccessorizer.WearableType,WearableAccessorizer.Wearable> CopyWearables(
            Dictionary<WearableAccessorizer.WearableType,WearableAccessorizer.Wearable> input)
        {
            var result=new Dictionary<WearableAccessorizer.WearableType,WearableAccessorizer.Wearable>();
            if(input!=null)foreach(var pair in input)
            {
                if(pair.Value==null)continue;
                var files=new List<KAnimFile>();
                if(pair.Value.AnimNames!=null)foreach(var name in pair.Value.AnimNames)
                    if(Assets.TryGetAnim(name,out var file))files.Add(file);
                // Native RestoreWearables mutates nested Wearables; clone them
                // rather than changing the real patient's stored appearance.
                result[pair.Key]=new WearableAccessorizer.Wearable(files,pair.Value.buildOverridePriority);
            }
            return result;
        }
        public void Clear()
        {
            if(display!=null){display.SetActive(false);Util.KDestroyGameObject(display);}
            display=null;anim=null;source=null;followCarrier=null;followHousing=null;currentPose=null;
        }
        protected override void OnCleanUp(){Clear();base.OnCleanUp();}
    }
}
