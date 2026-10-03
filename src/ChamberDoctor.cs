using System;
using TUNING;
namespace Baiye.Resurrection
{
    public sealed class ChamberDoctor : Workable,ISim200ms
    {
        Chore chore;
        TreatmentStage scheduledStage;
        ChamberProcess Process=>GetComponent<ChamberProcess>();
        public bool IsWorking=>worker!=null;
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();SetWorkTime(60);resetProgressOnStop=false;synchronizeAnims=false;
            requiredSkillPerk=Db.Get().SkillPerks.CanDoctor.Id;
            attributeConverter=Db.Get().AttributeConverters.DoctorSpeed;
            attributeExperienceMultiplier=DUPLICANTSTATS.ATTRIBUTE_LEVELING.BARELY_EVER_EXPERIENCE;
            skillExperienceSkillGroup=Db.Get().SkillGroups.MedicalAid.Id;skillExperienceMultiplier=SKILLS.BARELY_EVER_EXPERIENCE;
            overrideAnims=new[]{Assets.GetAnim("anim_interacts_research_center_kanim")};
            workAnims=new HashedString[]{"working_pre","working_loop"};faceTargetWhenWorking=true;
            SetOffsets(new[]{new CellOffset(-2,0),new CellOffset(3,0)});
        }
        public void Sim200ms(float dt)
        {
            var p=Process;
            if(chore!=null&&(chore.isComplete||!chore.IsValid()))chore=null;
            // A doctor already at the console must also stop at a schedule
            // boundary. Initial chore eligibility alone does not enforce this.
            if(chore!=null&&worker!=null&&worker.GetComponent<Schedulable>()?.IsAllowed(Db.Get().ScheduleBlockTypes.Work)==false)Cancel();
            if(chore!=null&&(!p.CanDoctor||scheduledStage!=p.Model.Stage))Cancel();
            if(chore==null&&p.CanDoctor)
            {
                SetWorkTime(60);WorkTimeRemaining=(float)Math.Max(.01,p.Model.DoctorRemaining);scheduledStage=p.Model.Stage;
                chore=new WorkChore<ChamberDoctor>(Db.Get().ChoreTypes.Doctor,this,run_until_complete:true,ignore_schedule_block:false,only_when_operational:true,is_preemptable:true);
            }
        }
        protected override bool OnWorkTick(WorkerBase worker,float dt)
        {
            if(Process.Model.Stage==scheduledStage)Process.Model.DoctorRemaining=Math.Max(0,WorkTimeRemaining);
            Process.Sequence.DoctorProgress(scheduledStage,Process.Model.DoctorRemaining);
            return !Process.CanDoctor||base.OnWorkTick(worker,dt);
        }
        protected override void OnStopWork(WorkerBase worker)
        {
            if(Process.Model.Stage==scheduledStage)Process.Model.DoctorRemaining=Math.Max(0,WorkTimeRemaining);
            base.OnStopWork(worker);
        }
        protected override void OnCompleteWork(WorkerBase worker)
        {
            base.OnCompleteWork(worker);bool completed=Process.CanDoctor&&WorkTimeRemaining<=0;
            chore=null;
            if(completed){Process.Model.DoctorRemaining=0;Process.DoctorFinished();}
        }
        public void Cancel(){chore?.Cancel("Medical stage paused");chore=null;}
        protected override void OnCleanUp(){Cancel();base.OnCleanUp();}
    }
    public sealed class ChamberPatientReturn : Workable,ISim200ms
    {
        Chore chore;
        ChamberProcess Process=>GetComponent<ChamberProcess>();
        public static readonly Chore.Precondition SelectedPatient=new Chore.Precondition
        {
            id="BaiyeSelectedRecoveringPatient",description="Selected recovering patient",canExecuteOnAnyThread=false,
            fn=(ref Chore.Precondition.Context context,object data)=>context.consumerState.gameObject==((ChamberProcess)data).Target
        };
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();SetWorkTime(1);resetProgressOnStop=false;synchronizeAnims=false;
            overrideAnims=new[]{Assets.GetAnim("anim_interacts_research_center_kanim")};
            workAnims=new HashedString[]{"working_pre","working_loop"};SetOffsets(new[]{new CellOffset(-2,0),new CellOffset(3,0)});
        }
        public void Sim200ms(float dt)
        {
            if(chore!=null&&(chore.isComplete||!chore.IsValid()))chore=null;
            if(chore!=null&&!Process.NeedsPatientReturn)Cancel();
            if(chore==null&&Process.NeedsPatientReturn&&!Process.ReturnReady)
            {
                SetWorkTime(1);chore=new WorkChore<ChamberPatientReturn>(Db.Get().ChoreTypes.Heal,this,ignore_schedule_block:true,only_when_operational:false,is_preemptable:true,ignore_building_assignment:true);
                chore.AddPrecondition(SelectedPatient,Process);
            }
        }
        protected override void OnCompleteWork(WorkerBase worker){base.OnCompleteWork(worker);chore=null;Process.ReturnReady=true;}
        public void Cancel(){chore?.Cancel("Recovery admission cancelled");chore=null;}
        protected override void OnCleanUp(){Cancel();base.OnCleanUp();}
    }
}
