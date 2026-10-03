using System;
using UnityEngine;
namespace Baiye.Resurrection
{
    // Only a render proxy travels with the carrier. The original stored minion
    // stays in the chamber until a successful deposit, including across saves.
    public sealed class ChamberCarry : Workable,ISim200ms
    {
        Chore chore;
        public ChamberProcess Process=>GetComponent<ChamberProcess>();
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();SetOffsets(new[]{new CellOffset(-2,0),new CellOffset(3,0)});
        }
        public void Sim200ms(float dt)
        {
            if(chore!=null&&(chore.isComplete||!chore.IsValid())){chore=null;Process.InterruptCarry();}
            if(chore!=null&&!Process.HasStasis){chore.Cancel("Patient already released");chore=null;}
            if(chore==null&&Process.NeedsCarrier&&PatientTransfer.ExitCell(gameObject)!=Grid.InvalidCell)chore=new ChamberCarryChore(this);
        }
        protected override void OnCleanUp(){chore?.Cancel("Chamber cleanup");chore=null;base.OnCleanUp();}
    }
    public sealed class ChamberCarryChore : Chore<ChamberCarryChore.StatesInstance>
    {
        readonly ChamberCarry chamber;
        GameObject carrier;
        bool carrying;
        public sealed class StatesInstance : GameStateMachine<States,StatesInstance,ChamberCarryChore,object>.GameInstance
        {
            public int Destination=Grid.InvalidCell;
            public StatesInstance(ChamberCarryChore master):base(master){}
        }
        public sealed class States : GameStateMachine<States,StatesInstance,ChamberCarryChore>
        {
            public TargetParameter mover;
            public TargetParameter pod;
            public ApproachSubState<IApproachable> approach;
            public State pickup;
            public State transport;
            public State deposit;
            public State success;
            public State failure;
            public override void InitializeStates(out BaseState default_state)
            {
                default_state=approach;Target(mover);
                root.Update((smi,dt)=>
                {
                    var master=smi.master;
                    if(!master.chamber.Process.HasStasis||master.carrier==null||master.carrier.GetComponent<Schedulable>()?.IsAllowed(Db.Get().ScheduleBlockTypes.Work)==false)
                        smi.GoTo(failure);
                });
                approach.InitializeStates(mover,pod,pickup,failure);
                pickup.Enter(smi=>
                {
                    var master=smi.master;
                    if(!master.chamber.Process.BeginCarry(master.carrier)){smi.GoTo(failure);return;}
                    master.carrying=true;master.SetOverride(true);
                    smi.Destination=master.FindDestination();
                    if(smi.Destination==Grid.InvalidCell)smi.GoTo(failure);
                }).PlayAnim("pickup").EventTransition(GameHashes.AnimQueueComplete,transport);
                transport.MoveTo(smi=>smi.Destination,deposit,failure);
                deposit.PlayAnim("place").EventHandler(GameHashes.AnimQueueComplete,smi=>
                {
                    if(smi.master.chamber.Process.FinishCarry(smi.master.carrier,Grid.PosToCell(smi.master.carrier)))smi.GoTo(success);
                    else smi.GoTo(failure);
                });
                success.Enter(smi=>{smi.SetStatus(Status.Success);smi.StopSM("Patient placed safely");});
                failure.Enter(smi=>{smi.SetStatus(Status.Failed);smi.StopSM("Patient transport interrupted");});
            }
        }
        static readonly Precondition ReachPod=new Precondition
        {
            id="BaiyeReachDischargePod",description="Reach patient chamber and safe floor",canExecuteOnAnyThread=false,
            fn=(ref Precondition.Context context,object data)=>
            {
                var carry=(ChamberCarry)data;var p=carry.Process;
                if(!p.HasStasis||!p.Sequence.CarrierEligible(p.Carrier==context.consumerState.gameObject))return false;
                if(p.Sequence.Phase==PodPhase.Carrying)return true;
                int origin=Grid.PosToCell(carry.gameObject),cost=int.MaxValue;
                foreach(int x in new[]{-2,3})
                {
                    int c=context.consumerState.navigator.GetNavigationCost(Grid.OffsetCell(origin,new CellOffset(x,0)));
                    if(c>=0)cost=Math.Min(cost,c);
                }
                if(cost==int.MaxValue)return false;context.cost+=cost;return true;
            }
        };
        public ChamberCarryChore(ChamberCarry chamber):base(Db.Get().ChoreTypes.DoctorFetch,chamber,(ChoreProvider)null,
            run_until_complete:true,is_preemptable:true,master_priority_class:PriorityScreen.PriorityClass.basic)
        {
            this.chamber=chamber;smi=new StatesInstance(this);
            AddPrecondition(ReachPod,chamber);
            AddPrecondition(ChorePreconditions.instance.IsScheduledTime,Db.Get().ScheduleBlockTypes.Work);
        }
        public override void Begin(Precondition.Context context)
        {
            carrier=context.consumerState.gameObject;smi.sm.mover.Set(carrier,smi);smi.sm.pod.Set(chamber,smi);base.Begin(context);
        }
        int FindDestination()
        {
            int current=Grid.PosToCell(carrier),origin=Grid.PosToCell(chamber.gameObject),world=chamber.GetMyWorldId();
            var nav=carrier.GetComponent<Navigator>();
            // A short real walk to a nearby floor outside the bay; test every
            // candidate against this particular carrier's navigation graph.
            foreach(int x in new[]{-3,4,-4,5,-2,3})
            {
                int cell=Grid.OffsetCell(origin,new CellOffset(x,0));
                if(cell!=current&&PatientTransfer.IsSafeCell(cell,world)&&nav.GetNavigationCost(cell)>=0)return cell;
            }
            return PatientTransfer.IsSafeCell(current,world)?current:Grid.InvalidCell;
        }
        void SetOverride(bool add)
        {
            if(carrier==null)return;
            var anim=carrier.GetComponent<KAnimControllerBase>();
            var file=Assets.GetAnim("anim_incapacitated_carrier_kanim");
            if(add)anim.AddAnimOverrides(file);else anim.RemoveAnimOverrides(file);
        }
        protected override void End(string reason)
        {
            if(carrying){SetOverride(false);chamber.Process.InterruptCarry();carrying=false;}
            base.End(reason);
        }
    }
}
