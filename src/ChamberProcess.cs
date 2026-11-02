using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class ChamberProcess : KMonoBehaviour,ISim200ms,ISaveLoadable
    {
        [Serialize] public TreatmentModel Model=new TreatmentModel();
        [Serialize] public Ref<KPrefabID> Selected;
        [Serialize] public bool Requested;
        [Serialize] public bool ReturnReady;
        [Serialize] public bool RescueInbound;
        [Serialize] public string PatientName;
        [Serialize] public ChamberSequence Sequence=new ChamberSequence();
        public GameObject Carrier { get; private set; }
        Storage[] stores;
        MinionStorage stasis;
        Operational op;
        EnergyConsumer energy;
        FetchChore fetch;
        bool spawned;
        int controlsState=-1;
        Tag claimTag;
        FoodSupplyPlan foodPlan;
        double sourceFoodKcal,sourceKcalPerKg;
        float nextFoodScan;
        ManualDeliveryKG[] deliveries;
        readonly NativeMaterialDelivery[] materialPorts=new NativeMaterialDelivery[2];
        readonly MaterialSupplyPlan[] materialPlans=new MaterialSupplyPlan[2];
        readonly string[] materialDiagnosticKeys=new string[2];
        readonly float[] materialDiagnosticTimes={float.NegativeInfinity,float.NegativeInfinity};
        readonly object[] lastMaterialTasks=new object[2];
        readonly string[] materialEvents={"initial","initial"};
        static readonly Dictionary<int,ChamberProcess> Claims=new Dictionary<int,ChamberProcess>();
        public string Block { get; private set; }="";
        public float LastTreatmentTime { get; private set; }=float.NegativeInfinity;
        public float LastReleaseTime { get; private set; }=float.NegativeInfinity;
        public bool Occupied=>Model.Stage!=TreatmentStage.Empty||Requested;
        public bool Automatic=>Model.IsAutomatic;
        public bool Operational=>op!=null&&op.IsOperational;
        public GameObject Target=>Selected?.Get()?.gameObject;
        public float RequiredWatts=>Model.Stage==TreatmentStage.Rebuilding?2400:Model.Stage==TreatmentStage.Recovering?1200:0;
        public bool HasStasis=>stasis!=null&&stasis.GetStoredMinionInfo().Count==1;
        public bool HasBody=>stores!=null&&stores[0].items.Any(x=>x!=null&&x.GetComponent<MinionIdentity>()!=null);
        public GameObject VisualPatient
        {
            get
            {
                if(HasStasis)return stasis.GetStoredMinionInfo()[0].serializedMinion?.Get()?.gameObject;
                return stores==null?null:stores[0].items.FirstOrDefault(x=>x!=null&&x.GetComponent<MinionIdentity>()!=null);
            }
        }
        public bool CanDoctor=>spawned&&Model.NeedsDoctor&&Sequence.ExamAllowed&&Operational&&(Model.Stage!=TreatmentStage.StartCheck||StartupReady)&& (Model.Stage!=TreatmentStage.DischargeCheck||PatientTransfer.ExitCell(gameObject)!=Grid.InvalidCell)&& (Model.Stage==TreatmentStage.StartCheck?HasBody:HasStasis);
        public bool NeedsCarrier=>spawned&&HasStasis&&Sequence.Phase==PodPhase.AwaitingCarrier;
        public bool NeedsPatientReturn=>Requested&&Target!=null&&!Target.HasTag(GameTags.Corpse)&&Target.GetComponent<PatientRecord>()?.NeedsRecovery==true;
        public bool StartupReady=>SupplyRequirements.Missing(Supplies(),SupplyRequirements.Startup(Model),false)=="";
        double Water=>stores[1].GetMassAvailable(SimHashes.Water);
        double Oxygen=>stores[2].GetMassAvailable(SimHashes.Oxygen);
        double Metal=>stores[3].GetMassAvailable(new Tag("RefinedMetal"));
        double Plastic=>stores[4].GetMassAvailable(SimHashes.Polypropylene);
        public double FoodKcal=>stores==null?0:stores[5].items.Where(UsableFood).Sum(x=>(double)x.GetComponent<Edible>().Calories/1000);
        static bool UsableFood(GameObject item)
        {
            if(item==null)return false;
            var e=item.GetComponent<Edible>();var rot=item.GetSMI<Rottable.Instance>();
            return e!=null&&e.FoodInfo!=null&&e.GetQuality()>=2&&e.Calories>0&&!float.IsInfinity(e.Calories)&&(rot==null||rot.RotValue>0);
        }
        public string TargetName=>Target?.GetProperName()??ChamberStrings.T("未选择","None selected");
        protected override void OnSpawn()
        {
            base.OnSpawn();stores=GetComponents<Storage>();stasis=GetComponent<MinionStorage>();op=GetComponent<Operational>();energy=GetComponent<EnergyConsumer>();
            deliveries=GetComponents<ManualDeliveryKG>();for(int i=0;i<2&&i<deliveries.Length;i++)materialPorts[i]=new NativeMaterialDelivery(deliveries[i]);
            Model=Model??new TreatmentModel();Sequence=Sequence??new ChamberSequence();Sequence.Restore(Model);claimTag=new Tag("BaiyePatientClaim_"+GetComponent<KPrefabID>().InstanceID);
            spawned=true;
            if(Requested&&Target!=null&&!HasStasis&&!Reserve(Target)){Requested=false;Selected=null;Model.Reset();Sequence.Reset();}
            Subscribe((int)GameHashes.QueueDestroyObject,OnDestroyed);
            Status("STATE",()=>StateTitle()+" · "+(PatientName??TargetName),()=>Block);
            Status("BLOCK",()=>Block==""?ChamberStrings.T("条件满足","Requirements met"):Block,()=>SupplyDetails());
            Status("PROGRESS",()=>ChamberStrings.T("重建 / 康复：","Rebuild / recovery: ")+Model.RebuildTime.ToString("0")+"/1800 s · "+Model.RecoveryTime.ToString("0")+"/3000 s",()=>ChamberStrings.T("营养已付：","Nutrition paid: ")+Model.NutritionPaid.ToString("0")+"/"+Model.NutritionTarget.ToString("0")+" kcal");
            Status("DOCTOR",()=>Sequence.DischargeAuthorized?ChamberStrings.T("出仓检查完成，等待搬运","Discharge approved; awaiting transport"):Model.NeedsDoctor?ChamberStrings.T("医生检查剩余：","Examination remaining: ")+Model.DoctorRemaining.ToString("0.0")+" s":ChamberStrings.T("自动阶段，无需医生驻守","Automatic stage; no attending doctor"),()=>ChamberStrings.T("医生遵守工作、睡眠与吃饭安排。离岗与换班保留检查和液面进度；自动治疗无需驻守。","Examinations respect work, sleep and meal schedules. Exam and fluid progress survives interruptions; automatic treatment needs no attending doctor."));
            Status("POD",()=>ChamberStrings.T("舱门 / 治疗液：","Door / treatment fluid: ")+(Sequence.Door*100).ToString("0")+"% / "+(Sequence.Fluid*100).ToString("0")+"%",()=>ChamberStrings.T("0% 舱门表示完全打开；治疗液为仓内循环，无需液体排出口。","Door 0% is fully open. Treatment fluid circulates internally; no liquid output is required."));
            Status("STOCK",()=>ChamberStrings.T("水 / 氧 / 营养：","Water / O2 / food: ")+Water.ToString("0.0")+" kg · "+Oxygen.ToString("0.0")+" kg · "+FoodKcal.ToString("0")+" kcal",()=>ChamberStrings.T("费用倍数：","Cost multiplier: ")+Model.Multiplier.ToString("0.0")+ChamberStrings.T("；空仓前可移除未用物资。错误管道物质不计入治疗。","; wrong pipe elements do not count as supplies."));
            Status("FOOD",FoodDeliveryStatus,FoodDetails);
            Status("METAL",()=>MaterialDeliveryStatus(0),()=>MaterialDeliveryDetails(0));
            Status("PLASTIC",()=>MaterialDeliveryStatus(1),()=>MaterialDeliveryDetails(1));
        }
        string StateTitle()
        {
            switch(Sequence.Phase)
            {
                case PodPhase.Closing:return ChamberStrings.T("正在关闭舱门","Closing chamber door");
                case PodPhase.Opening:return ChamberStrings.T("治疗液已回收，正在开门","Fluid recovered; opening door");
                case PodPhase.AwaitingCarrier:return ChamberStrings.T("等待复制人搬出病人","Awaiting patient carrier");
                case PodPhase.Carrying:return ChamberStrings.T("病人正在搬运出仓","Transporting patient");
                default:return ChamberStrings.StageName(Model.Stage);
            }
        }
        string SupplyDetails()
        {
            var need=RequiredSupplies();
            string line(string zh,string en,double have,double required,string unit)=>ChamberStrings.T(zh,en)+": "+have.ToString("0.###")+" / "+required.ToString("0.###")+" "+unit;
            return ChamberStrings.T("现有 / 本阶段最低需求：\n","Available / minimum for this stage:\n")+
                string.Join("\n",new[]{line("氧气","Oxygen",Oxygen,need.Oxygen,"kg"),line("水","Water",Water,need.Water,"kg"),line("精炼金属","Refined metal",Metal,need.Metal,"kg"),line("塑料","Plastic",Plastic,need.Plastic,"kg"),line("品质 ≥ +2 营养食品","Quality ≥ +2 food",FoodKcal,need.Nutrition,"kcal")})+
                ChamberStrings.T("\n氧气必须从气体输入口输送；周围空气不计入储备。水使用液体输入口；固体和营养食品由复制人搬运。","\nSupply oxygen through the gas input; surrounding air does not count. Water uses the liquid input; duplicants deliver solids and food.");
        }
        void Status(string id,Func<string> title,Func<string> tip)
        {
            var item=new StatusItem("BAIYE_RECONSTRUCTION_"+id,"{TITLE}","{TIP}","",StatusItem.IconType.Info,NotificationType.Neutral,false,OverlayModes.None.ID,showWorldIcon:false);
            item.resolveStringCallback=(text,data)=>text.Replace("{TITLE}",title()).Replace("{TIP}",tip());GetComponent<KSelectable>().AddStatusItem(item,this);
        }
        public void CycleTarget()
        {
            if(Occupied)return;
            var candidates=Components.MinionIdentities.Items.Where(x=>PatientTransfer.Eligible(x)&&!Rescue.WatcherStation.IsClaimed(x.gameObject)&&x.GetMyWorldId()==this.GetMyWorldId()&&ClaimAvailable(x.GetComponent<KPrefabID>().InstanceID)).OrderBy(x=>x.GetComponent<KPrefabID>().InstanceID).ToList();
            int current=candidates.FindIndex(x=>x.gameObject==Target);
            Selected=candidates.Count==0?null:new Ref<KPrefabID>(candidates[(current+1)%candidates.Count].GetComponent<KPrefabID>());
            Refresh();
        }
        static bool ClaimAvailable(int id)=>!Claims.TryGetValue(id,out var p)||p==null||!p.Requested;
        public static bool IsClaimedByChamber(GameObject body)
        {
            var id=body?.GetComponent<KPrefabID>();return id!=null&&!ClaimAvailable(id.InstanceID);
        }
        public bool TryReserveRescueBody(GameObject body)
        {
            if(!spawned||Occupied||body==null||!body.HasTag(GameTags.Corpse)||body.GetMyWorldId()!=this.GetMyWorldId()||GetComponent<Deconstructable>()?.IsMarkedForDeconstruction()==true)return false;
            var identity=body.GetComponent<MinionIdentity>();if(identity==null||identity.model!=MinionConfig.MODEL||!Reserve(body))return false;
            Selected=new Ref<KPrefabID>(body.GetComponent<KPrefabID>());Requested=true;RescueInbound=true;PatientName=body.GetProperName();
            GetComponent<Deconstructable>()?.SetAllowDeconstruction(false);Refresh();return true;
        }
        public void CancelRescueReservation(GameObject body)
        {
            if(!RescueInbound||body!=Target)return;Unreserve();Selected=null;Requested=false;RescueInbound=false;PatientName=null;Refresh();
        }
        public bool TryAcceptReservedRescueBody(GameObject body,Storage source)
        {
            if(!spawned||!RescueInbound||!Requested||body==null||body!=Target||!body.HasTag(GameTags.Corpse)||source==null||!source.items.Contains(body)||HasBody||HasStasis)return false;
            try {
                source.Drop(body,false);stores[0].Store(body,true,false,false);
                if(!stores[0].items.Contains(body))throw new InvalidOperationException("Rescue transfer failed");
                RescueInbound=false;StopFetch();Refresh();return true;
            } catch(Exception e) {
                Debug.LogWarning("[BaiyeResurrectionChamber] Rescue handoff failed: "+e);
                if(stores[0].items.Contains(body))stores[0].Drop(body,false);
                if(!source.items.Contains(body))source.Store(body,true,false,false);
                return false;
            }
        }
        bool Reserve(GameObject target)
        {
            int id=target.GetComponent<KPrefabID>().InstanceID;
            if(Claims.TryGetValue(id,out var other)&&other!=null&&other!=this&&other.Requested)return false;
            Claims[id]=this;target.GetComponent<KPrefabID>().AddTag(claimTag);return true;
        }
        void Unreserve()
        {
            var target=Target;if(target==null)return;
            int id=target.GetComponent<KPrefabID>().InstanceID;
            if(Claims.TryGetValue(id,out var p)&&p==this)Claims.Remove(id);
            target.GetComponent<KPrefabID>().RemoveTag(claimTag);
        }
        public bool Request()
        {
            if(Occupied||Target==null||Rescue.WatcherStation.IsClaimed(Target)||GetComponent<Deconstructable>()?.IsMarkedForDeconstruction()==true||!PatientTransfer.Eligible(Target.GetComponent<MinionIdentity>())||!Reserve(Target))return false;
            Requested=true;PatientName=TargetName;
            GetComponent<Deconstructable>()?.SetAllowDeconstruction(false);
            if(Target.HasTag(GameTags.Corpse))CreateFetch();
            Refresh();return true;
        }
        void CreateFetch()
        {
            if(fetch!=null||Target==null||RescueInbound)return;
            fetch=new FetchChore(Db.Get().ChoreTypes.FetchCritical,stores[0],TUNING.DUPLICANTSTATS.STANDARD.BaseStats.DEFAULT_MASS,new HashSet<Tag>{claimTag},FetchChore.MatchCriteria.MatchTags,GameTags.Corpse,operational_requirement:global::Operational.State.Functional);
            fetch.allowMultifetch=false;
        }
        void StopFetch(){fetch?.Cancel("Patient fetch finished or cancelled");fetch=null;}
        public void Sim200ms(float dt)
        {
            if(!spawned)return;
            if(Requested&&!HasStasis&&Target==null&&!HasBody){Requested=false;Model.Reset();Sequence.Reset();StopFetch();Block=ChamberStrings.T("目标遗体已不存在","Selected body no longer exists");}
            if(Requested&&Model.Stage==TreatmentStage.Empty&&HasBody)
            {
                var body=stores[0].items.First(x=>x!=null&&x.GetComponent<MinionIdentity>()!=null);
                if(body!=Target)throw new InvalidOperationException("Unexpected body delivered");
                Model.Admit(body.GetComponent<PatientRecord>().SuccessfulRevivals);Sequence.Admit();StopFetch();
            }
            if(ReturnReady&&NeedsPatientReturn)
            {
                ReturnReady=false;var target=Target;Model.Resume(target.GetComponent<PatientRecord>().Recovery);
                Unreserve();PatientTransfer.EnterStasis(stasis,target,Model,false);Selected=null;Sequence.Admit();
            }
            if(Model.Stage==TreatmentStage.RecoveryCheck&&!HasStasis&&HasBody)
            {
                var body=stores[0].items.First(x=>x!=null&&x.GetComponent<MinionIdentity>()!=null);
                Unreserve();stores[0].Drop(body);PatientTransfer.EnterStasis(stasis,body,Model,true);Selected=null;
            }
            if(Requested&&Model.Stage==TreatmentStage.Empty&&Target!=null&&Target.HasTag(GameTags.Corpse)&&!HasBody)CreateFetch();
            UpdateDeliveries();
            Sequence.Tick(dt,Operational);
            Sequence.DoctorProgress(Model.Stage,Model.DoctorRemaining);
            energy.BaseWattageRating=RequiredWatts>0?RequiredWatts:1200;
            var bill=Model.Preview(.2);bool ready=Sequence.TreatmentAllowed&&bill.Seconds>0&&CanPay(bill);
            op.SetActive(Operational&&ready);
            string missing=SupplyRequirements.Missing(Supplies(),RequiredSupplies(),ChamberStrings.Chinese);
            if(Sequence.Phase==PodPhase.Carrying)Block=ChamberStrings.T("搬运中；病人仍受安全托管","Transport in progress; patient protected");
            else if(NeedsCarrier)Block=PatientTransfer.ExitCell(gameObject)==Grid.InvalidCell?ChamberStrings.T("没有安全出仓地面；等待搬运","No safe exit floor; awaiting transport"):ChamberStrings.T("等待可抵达舱门的复制人搬运","Awaiting a duplicant able to reach the door");
            else if(missing!="")Block=missing;
            else if(!Operational&&Occupied)Block=ChamberStrings.T("等待电力或自动化允许运行","Waiting for power or automation permission");
            else if(Sequence.Phase==PodPhase.Closing||Sequence.Phase==PodPhase.Opening)Block=StateTitle();
            else if(Model.NeedsDoctor&&!Sequence.DischargeAuthorized)Block=DoctorBlock();
            else if(Model.IsAutomatic&&!ready)Block=ChamberStrings.T("等待治疗液充满","Waiting for treatment fluid to fill");
            else if(Requested&&Model.Stage==TreatmentStage.Empty)Block=ChamberStrings.T("等待指定病人入仓","Waiting for the selected patient");
            else Block="";
            if(Operational&&!Sequence.DischargeAuthorized&&(Model.Stage==TreatmentStage.Recovering||Model.Stage==TreatmentStage.DischargeCheck)&&(Water<=1e-6||Oxygen<=1e-6))
            {
                Model.LifeSupportLost(dt);Sequence.RefillForRecovery();
            }
            GetComponent<LogicPorts>()?.SendSignal(ChamberStrings.AttentionPort,Occupied&&(Model.NeedsDoctor||NeedsCarrier||(Model.IsAutomatic&&!ready))?1:0);
            // Discharge first; normal deconstruction is permitted once empty.
            var demolition=GetComponent<Deconstructable>();if(demolition!=null&&demolition.allowDeconstruction==Occupied)demolition.SetAllowDeconstruction(!Occupied);
            int controls=(int)Model.Stage*8+(HasStasis?4:0)+(Requested?2:0)+(PatientTransfer.ExitCell(gameObject)!=Grid.InvalidCell?1:0);
            if(controls!=controlsState){controlsState=controls;Refresh();}
        }
        string DoctorBlock()
        {
            if(Model.Stage==TreatmentStage.DischargeCheck&&PatientTransfer.ExitCell(gameObject)==Grid.InvalidCell)return ChamberStrings.T("缺少安全出仓地面（需要地板和两格净空）","No safe exit floor (floor and two cells of clearance required)");
            if(!(Model.Stage==TreatmentStage.StartCheck?HasBody:HasStasis))return ChamberStrings.T("等待病人入仓","Awaiting patient admission");
            if(GetComponent<ChamberDoctor>()?.IsWorking==true)return Model.DoctorRemaining<=20&&Model.Stage!=TreatmentStage.RecoveryCheck?ChamberStrings.T(Model.Stage==TreatmentStage.DischargeCheck?"医生正在回收治疗液":"医生正在填充治疗液",Model.Stage==TreatmentStage.DischargeCheck?"Doctor draining treatment fluid":"Doctor filling treatment fluid"):ChamberStrings.T("医生正在检查","Doctor examining patient");
            var doctors=Components.MinionIdentities.Items.Where(x=>x.model==MinionConfig.MODEL&&!x.gameObject.HasTag(GameTags.Corpse)&&!x.gameObject.HasTag(GameTags.Stored)&&x.GetMyWorldId()==this.GetMyWorldId()&&x.GetComponent<MinionResume>()?.HasPerk(Db.Get().SkillPerks.CanDoctor.Id)==true).ToList();
            if(doctors.Count==0)return ChamberStrings.T("缺少具有医疗技能的医生","No duplicant with the doctor skill");
            var working=doctors.Where(x=>x.GetComponent<Schedulable>()?.IsAllowed(Db.Get().ScheduleBlockTypes.Work)==true).ToList();
            if(working.Count==0)return ChamberStrings.T("医生处于睡眠 / 休息时间；检查进度保留","Doctors asleep or off duty; exam progress retained");
            int origin=Grid.PosToCell(gameObject);
            if(!working.Any(x=>new[]{-2,3}.Any(offset=>x.GetComponent<Navigator>()?.GetNavigationCost(Grid.OffsetCell(origin,new CellOffset(offset,0)))>=0)))return ChamberStrings.T("医生无法抵达操作位置","Doctors cannot reach the operating position");
            return ChamberStrings.T("等待医生领取医疗任务（检查医疗优先级）","Awaiting doctor assignment (check medical priority)");
        }
        TreatmentSupplies Supplies()=>new TreatmentSupplies{Water=Water,Oxygen=Oxygen,Metal=Metal,Plastic=Plastic,Nutrition=FoodKcal};
        TreatmentSupplies RequiredSupplies()
        {
            if(Model.Stage==TreatmentStage.StartCheck)return SupplyRequirements.Startup(Model);
            var need=SupplyRequirements.Slice(Model.Preview(.2));
            if(!Sequence.DischargeAuthorized&&(Model.Stage==TreatmentStage.Recovering||Model.Stage==TreatmentStage.DischargeCheck))
            {
                need.Water=Math.Max(need.Water,.02);need.Oxygen=Math.Max(need.Oxygen,.004);
            }
            return need;
        }
        bool CanPay(Bill b)=>Supplies().Covers(b);
        internal void Settle(float watts,bool paid)
        {
            if(!spawned||!paid||!Operational||!Sequence.TreatmentAllowed||watts+1e-3<RequiredWatts)return;
            var bill=Model.Payable(.2,Supplies(),paid);if(bill.Seconds<=0)return;
            stores[1].ConsumeIgnoringDisease(SimHashes.Water.CreateTag(),(float)bill.Water);
            stores[2].ConsumeIgnoringDisease(SimHashes.Oxygen.CreateTag(),(float)bill.Oxygen);
            if(bill.Metal>0)stores[3].ConsumeIgnoringDisease(new Tag("RefinedMetal"),(float)bill.Metal);
            if(bill.Plastic>0)stores[4].ConsumeIgnoringDisease(SimHashes.Polypropylene.CreateTag(),(float)bill.Plastic);
            if(bill.Nutrition>0)ConsumeFood(bill.Nutrition);
            Model.Commit(bill);LastTreatmentTime=GameClock.Instance.GetTime();
            var temperatures=GameComps.StructureTemperatures;
            temperatures?.ProduceEnergy(temperatures.GetHandle(gameObject),watts*.2f/1000,"Medical reconstruction",.2f);
        }
        void ConsumeFood(double kcal)
        {
            foreach(var item in stores[5].items.ToArray())
            {
                if(!UsableFood(item))continue;var edible=item.GetComponent<Edible>();
                double available=edible.Calories/1000d; if(available<=0)continue;
                double take=Math.Min(kcal,available);float mass=(float)(item.GetComponent<PrimaryElement>().Mass*take/available);
                stores[5].ConsumeIgnoringDisease(item.GetComponent<KPrefabID>().PrefabTag,mass);kcal-=take;
                if(kcal<=1e-7)break;
            }
        }
        void UpdateDeliveries()
        {
            var d=deliveries;
            for(int i=0;i<2;i++)UpdateMaterialDelivery(i);
            var delivery=d[2];bool cleared=ClearInvalidFood();
            if(cleared)delivery.AbortDelivery("Invalid medical food removed");
            ScanFoodSources();
            var store=stores[5];double mass=store.GetMassAvailable(new Tag(ChamberStrings.FoodTag));
            foodPlan=FoodSupplyPlan.Create(FoodSupplyPlan.ReserveTarget(Model,Occupied,Sequence.DischargeAuthorized),
                FoodKcal,mass,store.ExactMassStored(),store.capacityKg,sourceKcalPerKg);
            // Disable native 1 kg refill gating: this adapter requests by heat.
            delivery.refillMass=0;delivery.capacity=(float)foodPlan.CapacityKg;
            delivery.Pause(!foodPlan.CanRequest,"Medical nutrition reserve satisfied or storage full");
            // Clears completed lists and respects native functional requirements.
            delivery.UpdateDeliveryState();
            if(foodPlan.CanRequest&&op.MeetsRequirements(global::Operational.State.Functional))delivery.RequestDelivery();
        }
        Tag MaterialTag(int index)=>index==0?new Tag("RefinedMetal"):SimHashes.Polypropylene.CreateTag();
        double MaterialMass(int index)=>index==0?Metal:Plastic;
        bool MaterialConfigured(int index)=>deliveries!=null&&index<deliveries.Length&&deliveries[index].RequestedItemTag==MaterialTag(index)&&ReferenceEquals(deliveries[index].DebugStorage,stores[index+3]);
        void UpdateMaterialDelivery(int index)
        {
            var store=stores[index+3];var required=RequiredSupplies();
            materialPlans[index]=MaterialSupplyPlan.Create(MaterialSupplyPlan.ReserveTarget(Model,Occupied,index==1),store.GetMassAvailable(MaterialTag(index)),store.ExactMassStored(),store.capacityKg,index==0?required.Metal:required.Plastic);
            if(index>=deliveries.Length){LogMaterialDelivery(index,"missing_component");return;}
            var delivery=deliveries[index];var before=delivery.DebugFetchList;
            if(!MaterialConfigured(index)){delivery.Pause(true,"Medical material delivery configuration mismatch");delivery.AbortDelivery("Medical material delivery configuration mismatch");materialEvents[index]="configuration_mismatch";}
            else
            {
                bool increased=materialPlans[index].CapacityKg-delivery.capacity>0.001;
                MaterialDeliveryControl.Apply(materialPorts[index],materialPlans[index],op.MeetsRequirements(global::Operational.State.Functional));
                var after=delivery.DebugFetchList;
                if(before!=after)materialEvents[index]=after==null?(before!=null&&before.IsComplete?"completed":"cancelled"):(increased&&before!=null?"retargeted_and_submitted":"submitted");
                else if(lastMaterialTasks[index]!=after)materialEvents[index]="native_task_changed";
            }
            lastMaterialTasks[index]=delivery.DebugFetchList;LogMaterialDelivery(index,materialEvents[index]);
        }
        string MaterialDeliveryState(int index)
        {
            var plan=materialPlans[index];
            if(plan.TargetKg<=0)return ChamberStrings.T("当前阶段无需补货","No refill needed in this stage");
            if(!MaterialConfigured(index))return ChamberStrings.T("配送配置异常，已记录日志","Delivery configuration error; logged");
            if(!plan.NeedsMaterial)return ChamberStrings.T("储备充足","Reserve ready");
            if(!plan.CanRequest)return ChamberStrings.T("材料仓已满，仍缺料","Storage full; material still missing");
            if(!op.MeetsRequirements(global::Operational.State.Functional))return ChamberStrings.T("配送等待建筑恢复可用","Delivery waiting for a functional building");
            var delivery=deliveries[index];var list=delivery.DebugFetchList;
            if(delivery.IsPaused)return ChamberStrings.T("配送暂停，已记录日志","Delivery paused; logged");
            if(list==null||list.IsComplete)return ChamberStrings.T("补货任务尚未生成，已记录日志","Refill task not created yet; logged");
            if(!list.InProgress)return ChamberStrings.T("补货任务已提交，等待领取","Refill task submitted; awaiting assignment");
            return ChamberStrings.T("正在配送","Delivery in progress");
        }
        string MaterialDeliveryStatus(int index)=>ChamberStrings.T(index==0?"精炼金属：":"塑料：",index==0?"Refined metal: ":"Plastic: ")+MaterialMass(index).ToString("0.###")+" / "+materialPlans[index].TargetKg.ToString("0.###")+" kg · "+MaterialDeliveryState(index);
        string MaterialDeliveryDetails(int index)=>MaterialDeliveryStatus(index)+ChamberStrings.T("\n数值为舱内库存 / 当前补货目标：启动前备齐整份耗材，重建期间目标随剩余治疗需求下降。基地库存需复制人搬入。任务已提交不代表已有复制人领取。","\nChamber stock / current refill target: prepare the full startup bill, then reduce the target with remaining reconstruction needs. Colony stock must be delivered. A submitted task is not an assigned task.")+ChamberStrings.T("\n材料仓总重 / 容量：","\nMaterial storage mass / capacity: ")+stores[index+3].ExactMassStored().ToString("0.###")+" / "+stores[index+3].capacityKg.ToString("0.###")+" kg"+ChamberStrings.T("\n搬运遵守工作日程、优先级与寻路；日志中的候选来源量不保证可达。","\nDelivery respects work schedules, priorities and paths; candidate source mass in the log does not guarantee reachability.");
        void LogMaterialDelivery(int index,string action)
        {
            var delivery=index<deliveries.Length?deliveries[index]:null;var task=delivery?.DebugFetchList;var plan=materialPlans[index];
            string key=Model.Stage+"|"+Occupied+"|"+plan.NeedsMaterial+"|"+plan.CanRequest+"|"+MaterialConfigured(index)+"|"+delivery?.IsPaused+"|"+op.IsFunctional+"|"+Operational+"|"+(task!=null?RuntimeHelpers.GetHashCode(task):0)+"|"+task?.IsComplete+"|"+task?.InProgress;
            float now=GameClock.Instance.GetTime(),elapsed=now-materialDiagnosticTimes[index];
            if(elapsed<5||(key==materialDiagnosticKeys[index]&&(!plan.NeedsMaterial||elapsed<30)))return;
            materialDiagnosticKeys[index]=key;materialDiagnosticTimes[index]=now;
            double candidate=0;int world=this.GetMyWorldId();Tag tag=MaterialTag(index);
            foreach(var item in Components.Pickupables.Items)
                if(item!=null&&item.GetMyWorldId()==world&&item.storage!=stores[index+3]&&item.gameObject.HasTag(tag)&&!item.gameObject.HasTag(GameTags.StoredPrivate)&&!item.gameObject.HasTag(GameTags.MarkedForMove))candidate+=Math.Max(0,item.UnreservedFetchAmount);
            Debug.Log(FormattableString.Invariant($"[BaiyeResurrectionChamber][MaterialDelivery] version=0.1.7 chamber={GetComponent<KPrefabID>().InstanceID} world={world} material={tag} stage={Model.Stage} requested={Requested} event={action} stockKg={MaterialMass(index):0.######} taggedKg={stores[index+3].GetMassAvailable(tag):0.######} targetKg={plan.TargetKg} nativeCapacityKg={delivery?.capacity} totalKg={stores[index+3].ExactMassStored()} storageLimitKg={stores[index+3].capacityKg} configured={MaterialConfigured(index)} requestedTag={delivery?.RequestedItemTag} paused={delivery?.IsPaused} functional={op.IsFunctional} operational={Operational} task={task!=null} complete={task?.IsComplete} inProgress={task?.InProgress} candidateUnreservedKg={candidate} taskOrders={task?.FetchOrders.Count}"));
        }
        bool ClearInvalidFood()
        {
            bool cleared=false;
            foreach(var item in stores[5].items.ToArray())
            {
                if(item==null||UsableFood(item))continue;
                // Preserve the physical material; disposal is a normal colony task.
                stores[5].Drop(item);cleared=true;
            }
            return cleared;
        }
        void ScanFoodSources()
        {
            float now=GameClock.Instance.GetTime();if(now<nextFoodScan)return;nextFoodScan=now+2;
            sourceFoodKcal=sourceKcalPerKg=0;int world=this.GetMyWorldId();
            foreach(var edible in Components.Edibles.Items)
            {
                if(edible==null||edible.GetMyWorldId()!=world||!UsableFood(edible.gameObject))continue;
                var pickup=edible.GetComponent<Pickupable>();var id=edible.GetComponent<KPrefabID>();
                if(pickup==null||pickup.storage==stores[5]||pickup.UnreservedFetchAmount<=0||!id.HasTag(new Tag(ChamberStrings.FoodTag))||id.HasTag(GameTags.StoredPrivate)||id.HasTag(GameTags.MarkedForMove))continue;
                double mass=edible.GetComponent<PrimaryElement>().Mass,kcal=edible.Calories/1000d;
                if(mass<=0)continue;
                sourceFoodKcal+=kcal;double density=kcal/mass;
                if(sourceKcalPerKg==0||density<sourceKcalPerKg)sourceKcalPerKg=density;
            }
        }
        string FoodDeliveryStatus()
        {
            if(foodPlan.TargetKcal<=0)return ChamberStrings.T("当前无需补充营养","No nutrition delivery needed");
            if(!foodPlan.NeedsFood)return ChamberStrings.T("营养储备充足","Nutrition reserve ready");
            if(!foodPlan.CanRequest)return ChamberStrings.T("食品仓已满；营养仍不足","Food storage full; nutrition still insufficient");
            if(!op.MeetsRequirements(global::Operational.State.Functional))return ChamberStrings.T("食品配送等待建筑恢复可用","Food delivery waiting for a functional building");
            var list=GetComponents<ManualDeliveryKG>()[2].DebugFetchList;
            if(list!=null&&list.InProgress)return ChamberStrings.T("正在配送营养食品","Delivering nutrition food");
            if(sourceFoodKcal<=0)return ChamberStrings.T("暂无未预约的合格食品供补货","No unreserved qualifying food for refill");
            return ChamberStrings.T("等待复制人领取食品配送任务","Awaiting nutrition delivery assignment");
        }
        string FoodDetails()=>ChamberStrings.T("有效营养 / 当前储备目标：","Valid nutrition / current reserve target: ")+FoodKcal.ToString("0")+" / "+foodPlan.TargetKcal.ToString("0")+" kcal"+
            ChamberStrings.T("\n食品仓总重 / 容量：","\nFood storage mass / capacity: ")+stores[5].ExactMassStored().ToString("0.###")+" / "+stores[5].capacityKg.ToString("0.###")+" kg"+
            ChamberStrings.T("\n仅品质 ≥ +2 的有效食品计入营养。食品仍按环境条件腐败；失效物品移出仓内，不返还营养。按启动储备分批补货；康复后段只补未付营养。等待领取时检查搬运优先级、道路和来源储箱优先级。","\nOnly valid quality ≥ +2 food counts. Food still rots under ambient conditions; invalid items leave storage without refund. Refill in startup-reserve batches, capped by unpaid recovery nutrition. Check hauling priority, paths and source storage priority if unassigned.");
        public bool DoctorFinished()
        {
            if(!CanDoctor||Model.DoctorRemaining>1e-8)return false;
            if(!Sequence.FinishExam(Model.Stage,Model.DoctorRemaining))return false;
            if(Model.Stage!=TreatmentStage.DischargeCheck)Model.CompleteDoctorCheck();
            Refresh();return true;
        }
        public bool BeginCarry(GameObject carrier){if(!NeedsCarrier||!Sequence.BeginCarry())return false;Carrier=carrier;return true;}
        public void InterruptCarry(){Carrier=null;Sequence.InterruptCarry();}
        public bool FinishCarry(GameObject carrier,int cell)
        {
            if(Carrier!=carrier||Sequence.Phase!=PodPhase.Carrying||!PatientTransfer.IsSafeCell(cell,this.GetMyWorldId()))return false;
            return ReleaseAt(false,cell);
        }
        public bool Release(bool emergency)
        {
            if(!HasStasis)return false;int cell=PatientTransfer.ExitCell(gameObject);if(cell==Grid.InvalidCell)return false;
            return ReleaseAt(emergency,cell);
        }
        bool ReleaseAt(bool emergency,int cell)
        {
            var patient=PatientTransfer.Release(stasis,Model,Grid.CellToPosCBC(cell,Grid.SceneLayer.Move),emergency);
            if(patient==null)return false;
            Unreserve();Selected=null;Requested=false;ReturnReady=false;RescueInbound=false;Model.Reset();Sequence.Reset();Carrier=null;StopFetch();
            LastReleaseTime=GameClock.Instance.GetTime();Refresh();return true;
        }
        public bool Cancel()
        {
            GetComponent<ChamberDoctor>()?.Cancel();
            if(HasStasis)return Release(true);
            StopFetch();GetComponent<ChamberPatientReturn>()?.Cancel();Unreserve();
            if(HasBody)stores[0].DropAll();Selected=null;Requested=false;ReturnReady=false;RescueInbound=false;Model.Reset();Sequence.Reset();Refresh();return true;
        }
        public void Refresh(){if(DetailsScreen.Instance?.target==gameObject)DetailsScreen.Instance.Refresh(gameObject);}
        void OnDestroyed(object data)
        {
            // Engine destruction/meteor bypasses the normal empty-before-demolition rule.
            if(HasStasis)
            {
                int cell=PatientTransfer.ExitCell(gameObject);if(cell==Grid.InvalidCell)cell=Grid.PosToCell(gameObject);
                PatientTransfer.Release(stasis,Model,Grid.CellToPosCBC(cell,Grid.SceneLayer.Move),true);
            }
        }
        protected override void OnCleanUp()
        {
            StopFetch();Unreserve();spawned=false;
            base.OnCleanUp();
        }
    }
}
