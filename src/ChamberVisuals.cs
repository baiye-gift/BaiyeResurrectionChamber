using UnityEngine;
using KSerialization;
namespace Baiye.Resurrection
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class ChamberVisuals : KMonoBehaviour,ISim200ms
    {
        string current;
        GameObject effects;
        KBatchedAnimController effectAnim;
        GameObject door,fluid;
        KBatchedAnimController doorAnim,fluidAnim;
        public void Sim200ms(float dt)
        {
            var p=GetComponent<ChamberProcess>();if(p==null)return;
            var housing=GetComponent<KBatchedAnimController>();var patient=p.VisualPatient;
            float now=GameClock.Instance.GetTime();
            string selected=ChamberVisualState.Select(p.Model.Stage,patient!=null,p.Operational,p.Block!="",now-p.LastTreatmentTime<.6f,now-p.LastReleaseTime<1.2f);
            if(selected!=current)
            {
                current=selected;housing.Play(selected,KAnim.PlayMode.Loop);
                if(effectAnim!=null)effectAnim.Play("fx_"+selected,KAnim.PlayMode.Loop);
            }
            GetComponent<ChamberPatientVisual>().Show(patient,housing,p.HasStasis&&p.Operational,p.Carrier);
            if(door==null){door=Overlay("BaiyePodDoor",housing,"pod_door",out doorAnim);fluid=Overlay("BaiyePodFluid",housing,"pod_fluid",out fluidAnim);}
            SetLevel(door,doorAnim,housing,(float)p.Sequence.Door,-.001f);
            SetLevel(fluid,fluidAnim,housing,(float)p.Sequence.Fluid,0);
            if(patient==null&&selected!="release")
            {
                if(effects!=null)effects.SetActive(false);
                return;
            }
            if(effects==null)
            {
                effects=new GameObject("BaiyeChamberMedicalEffects");effects.SetActive(false);effects.transform.SetParent(transform,false);
                effectAnim=effects.AddComponent<KBatchedAnimController>();effectAnim.AnimFiles=housing.AnimFiles;effectAnim.animScale=housing.animScale;
                effectAnim.animWidth=housing.animWidth;effectAnim.animHeight=housing.animHeight;
                effectAnim.sceneLayer=Grid.SceneLayer.BuildingFront;effectAnim.initialAnim="fx_"+selected;effectAnim.initialMode=KAnim.PlayMode.Loop;
            }
            var point=housing.PositionIncludingOffset;point.z=Grid.GetLayerZ(Grid.SceneLayer.BuildingFront);
            effects.transform.SetPosition(point);effects.SetActive(true);effectAnim.SetDirty();
        }
        GameObject Overlay(string name,KBatchedAnimController housing,string clip,out KBatchedAnimController anim)
        {
            var obj=new GameObject(name);obj.SetActive(false);obj.transform.SetParent(transform,false);
            anim=obj.AddComponent<KBatchedAnimController>();anim.AnimFiles=housing.AnimFiles;anim.animScale=housing.animScale;
            anim.animWidth=housing.animWidth;anim.animHeight=housing.animHeight;anim.sceneLayer=Grid.SceneLayer.BuildingFront;
            anim.initialAnim=clip;anim.initialMode=KAnim.PlayMode.Paused;return obj;
        }
        static void SetLevel(GameObject obj,KBatchedAnimController anim,KBatchedAnimController housing,float level,float depth)
        {
            if(level<=0){obj.SetActive(false);return;}
            var point=housing.PositionIncludingOffset;point.z=Grid.GetLayerZ(Grid.SceneLayer.BuildingFront)+depth;
            obj.transform.SetPosition(point);obj.SetActive(true);anim.SetPositionPercent(Mathf.Clamp01(level));anim.SetDirty();
        }
        protected override void OnCleanUp()
        {
            if(effects!=null){effects.SetActive(false);Util.KDestroyGameObject(effects);}
            if(door!=null){door.SetActive(false);Util.KDestroyGameObject(door);}
            if(fluid!=null){fluid.SetActive(false);Util.KDestroyGameObject(fluid);}
            effects=null;effectAnim=null;base.OnCleanUp();
        }
    }
}
