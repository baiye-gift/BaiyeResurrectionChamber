using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Baiye.Resurrection;
using KSerialization;
using Klei.AI;

// Uses actual game-managed save/API types, never pretends to run Unity or AI.
class Program
{
    static string managed;
    static int passed;
    static void Main(string[] args)
    {
        managed=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>{var p=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        try{Run();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}
    }
    [SerializationConfig(MemberSerialization.OptIn)]
    public class Envelope
    {
        [Serialize] public TreatmentModel Treatment;
        [Serialize] public RecoveryDebt Debt;
        [Serialize] public bool Requested;
        [Serialize] public string PatientName;
        [Serialize] public int Revivals;
        [Serialize] public ChamberSequence Sequence;
    }
    [SerializationConfig(MemberSerialization.OptIn)]
    public class WatcherMotionEnvelope {
        [Serialize] public Baiye.Resurrection.Rescue.WatcherAction Action;
        [Serialize] public float ActionSeconds,PickupX,PickupY,PickupScale,ExitProgress,BedEntryProgress;
        [Serialize] public int ExitCell;
        [Serialize] public bool PickupFlip,ExitReturning,ExitHealing,ActionEndpointHeld,ExitEndpointHeld,ExitReturnEndpointHeld,BedEntryEndpointHeld;
    }
    static T RoundTrip<T>(T value) where T:new()
    {
        Manager.Initialize();byte[] data,directory;
        using(var s=new MemoryStream()){using(var w=new BinaryWriter(s)){Serializer.Serialize(value,w);data=s.ToArray();}}
        using(var s=new MemoryStream()){using(var w=new BinaryWriter(s)){Manager.SerializeDirectory(w);directory=s.ToArray();}}
        Manager.DeserializeDirectory(new FastReader(directory));var restored=new T();Check(Deserializer.Deserialize(restored,new FastReader(data)),"deserialization failed");return restored;
    }
    static void Test(string name,System.Action action){action();passed++;Console.WriteLine("PASS "+name);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        Test("watcher has saveable custody, scoped protection and station",()=>
        {
            var asm=typeof(ChamberProcess).Assembly;
            var station=asm.GetType("Baiye.Resurrection.Rescue.WatcherStation");
            var marker=asm.GetType("Baiye.Resurrection.Rescue.WatcherCustody");
            Check(station!=null && marker!=null,"watcher native custody types absent");
            Check(typeof(ISaveLoadable).IsAssignableFrom(station) && typeof(ISaveLoadable).IsAssignableFrom(marker),"watcher custody not saveable");
            foreach(var f in new[]{"Target","DroneCell","Phase","Living","Corpse","Cargo","Destination","DeliveryCell","FallbackSource","CareSeconds","Action","ActionSeconds","ActionEndpointHeld","PickupX","PickupY","PickupScale","PickupFlip","ExitPatient","ExitCell","ExitProgress","ExitReturning","ExitHealing","ExitEndpointHeld","ExitReturnEndpointHeld","BedEntryProgress","BedEntryEndpointHeld"})Check(station.GetField(f)?.GetCustomAttributes().Any(a=>a.GetType().Name=="Serialize")==true,"missing saved station field "+f);
            Check(marker.GetField("Owner")?.GetCustomAttributes().Any(a=>a.GetType().Name=="Serialize")==true,"missing saved owner");
            Check(station.GetMethod("Owns")!=null,"custody predicate missing");
        });
        Test("watcher protections target exact native signatures and actual custody",()=>
        {
            var asm=typeof(ChamberProcess).Assembly;
            var marker=asm.GetType("Baiye.Resurrection.Rescue.WatcherCustody");
            var owns=asm.GetType("Baiye.Resurrection.Rescue.WatcherStation").GetMethod("Owns");
            var fallbackOwns=asm.GetType("Baiye.Resurrection.Rescue.WatcherFallback").GetMethod("Owns");
            Check(Calls(owns).Any(m=>m.Name=="Contains"),"station protection does not check stored membership");
            Check(Calls(fallbackOwns).Any(m=>m.Name=="Contains"),"fallback protection does not check stored membership");
            Check(Calls(marker.GetProperty("Protected").GetMethod).Any(m=>m.Name=="Owns"),"marker alone grants protection");
            Check(typeof(IncapacitationMonitor.Instance).GetMethod("Bleed",new[]{typeof(float),typeof(IncapacitationMonitor.Instance)})!=null,"bleed signature drift");
            Check(typeof(SuffocationMonitor).GetMethod("UpdateTimeUntilDeath",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(SuffocationMonitor.Instance),typeof(float)},null)!=null,"suffocation signature drift");
            Check(typeof(Health).GetMethod("Damage",new[]{typeof(float)})!=null,"damage signature drift");
            Check(typeof(DeathMonitor.Instance).GetMethod("Kill",new[]{typeof(Death)})!=null,"kill signature drift");
            foreach(var patch in new[]{"WatcherBleedPatch","WatcherSuffocationPatch","WatcherDamagePatch","WatcherDeathPatch"}) {
                var type=asm.GetType("Baiye.Resurrection.Rescue."+patch);Check(type!=null,"missing patch "+patch);
                Check(Calls(type.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)).Any(m=>m.Name=="IsProtected"),"unscoped patch "+patch);
            }
        });
        Test("watcher care and corpse handoff preserve native patient identity",()=>
        {
            var asm=typeof(ChamberProcess).Assembly;var station=asm.GetType("Baiye.Resurrection.Rescue.WatcherStation");
            var care=Calls(station.GetMethod("Care",BindingFlags.Instance|BindingFlags.NonPublic));
            Check(!care.Any(m=>m.DeclaringType==typeof(PatientTransfer)||m.DeclaringType==typeof(MinionStorage)),"care reconstructs patient");
            var handoff=Calls(typeof(ChamberProcess).GetMethod("TryAcceptReservedRescueBody"));
            Check(handoff.Any(m=>m.DeclaringType==typeof(Storage)&&m.Name=="Store"),"handoff does not store original corpse");
            Check(!handoff.Any(m=>m.DeclaringType==typeof(PatientTransfer)||m.DeclaringType==typeof(MinionStorage)),"handoff revives corpse");
        });
        Test("watcher routing and destruction use native custody APIs",()=>
        {
            var asm=typeof(ChamberProcess).Assembly;var station=asm.GetType("Baiye.Resurrection.Rescue.WatcherStation");
            var pass=Calls(station.GetMethod("Passable",BindingFlags.Instance|BindingFlags.NonPublic));
            Check(pass.Any(m=>m.DeclaringType==typeof(Grid)&&m.Name=="IsValidCellInWorld"),"flight is not restricted to world");
            Check(pass.Any(m=>m.Name=="IsInsideState"),"flight ignores actual door state");
            var pickup=Calls(station.GetMethod("Reserve",BindingFlags.Instance|BindingFlags.NonPublic));
            Check(pickup.Any(m=>m.DeclaringType==typeof(Pickupable)&&m.Name=="Reserve"),"native pickup not reserved");
            var patch=asm.GetType("Baiye.Resurrection.Rescue.WatcherStorageDestructionPatch");
            var calls=Calls(patch.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic));
            Check(calls.Any(m=>m.DeclaringType==typeof(Storage)&&m.Name=="Drop")&&calls.Any(m=>m.DeclaringType==typeof(Storage)&&m.Name=="Store"),"destruction does not transfer custody");
            Check(typeof(IEntityConfig).IsAssignableFrom(asm.GetType("Baiye.Resurrection.Rescue.WatcherFallbackConfig")),"no saveable fallback prefab config");
        });
        Test("watcher emergency time only advances from paid power callback",()=>
        {
            var asm=typeof(ChamberProcess).Assembly;var station=asm.GetType("Baiye.Resurrection.Rescue.WatcherStation");
            var settle=station.GetMethod("Settle",BindingFlags.Instance|BindingFlags.NonPublic);Check(settle!=null,"no paid settlement");
            var patch=asm.GetType("Baiye.Resurrection.ChamberPaidPower");
            Check(Calls(patch.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic)).Any(m=>m.DeclaringType==station&&m.Name=="Settle"),"paid circuit callback not connected");
        });
        Test("watcher preserves actual custody until animated safe discharge ends",()=>
        {
            var station=typeof(ChamberProcess).Assembly.GetType("Baiye.Resurrection.Rescue.WatcherStation");var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var care=Calls(station.GetMethod("Care",flags));var manual=Calls(station.GetMethod("ReleaseSafely"));
            Check(care.Any(m=>m.Name=="BeginExit")&&manual.Any(m=>m.Name=="BeginExit"),"care/manual release bypass animated departure");
            Check(!care.Concat(manual).Any(m=>m.DeclaringType==typeof(Storage)&&m.Name=="Drop"),"patient released before departure animation");
            var finish=station.GetMethod("FinishExit",flags);Check(finish!=null,"missing deferred discharge commit");var calls=Calls(finish);
            Check(calls.Any(m=>m.Name=="IsSafeExitCell")&&calls.Any(m=>m.Name=="CanFinishExit")&&calls.Any(m=>m.Name=="Drop"),"discharge lacks final phase/exit validation");
        });
        Test("native save retains pickup contact and reversed/held discharge progress",()=>
        {
            var original=new WatcherMotionEnvelope{Action=Baiye.Resurrection.Rescue.WatcherAction.Pickup,ActionSeconds=.4f,PickupX=21.25f,PickupY=35.01f,PickupScale=.005f,PickupFlip=true,ExitProgress=.75f,ExitCell=12345,ExitReturning=true,ExitHealing=true,ExitEndpointHeld=true,ActionEndpointHeld=true,ExitReturnEndpointHeld=true,BedEntryProgress=.6f,BedEntryEndpointHeld=true};
            var loaded=RoundTrip(original);
            Check(loaded.Action==original.Action&&loaded.ActionSeconds==.4f&&loaded.PickupX==21.25f&&loaded.PickupY==35.01f&&loaded.PickupScale==.005f&&loaded.PickupFlip,"native save loses source contact or action progress");
            Check(loaded.ExitProgress==.75f&&loaded.ExitCell==12345&&loaded.ExitReturning&&loaded.ExitHealing&&loaded.ExitEndpointHeld&&loaded.ActionEndpointHeld,"native save loses return direction or visible endpoint hold");
            Check(loaded.ExitReturnEndpointHeld&&loaded.BedEntryEndpointHeld&&loaded.BedEntryProgress==.6f,"native save loses entry or reverse endpoint holds");
        });
        Test("animated patient adapter uses exact native file and progressing frame",()=>
        {
            var show=Calls(typeof(ChamberPatientVisual).GetMethod("ShowAnimated"));
            Check(show.Any(m=>m.Name=="GetAnim")&&show.Any(m=>m.Name=="GetData")&&show.Any(m=>m.Name=="SetPositionPercent")&&show.Any(m=>m.Name=="Place"),"animated adapter bypasses native frame/file validation or production placement solver");
        });
        Test("manual discharge binds reversible native body blend without cloning identity",()=>
        {
            var visual=typeof(ChamberProcess).Assembly.GetType("Baiye.Resurrection.Rescue.WatcherVisual");var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var draw=Calls(visual.GetMethod("DrawExit",flags));
            Check(draw.Any(m=>m.Name=="EmergencyBlend")&&draw.Any(m=>m.Name=="EmergencyMotion")&&draw.Count(m=>m.Name=="ShowAnimated")==2&&draw.Any(m=>m.Name=="SetAnimatedOpacity"),"manual release or return still hard-cuts between native body views");
            var create=Calls(visual.GetMethod("Create",flags));Check(!create.Any(m=>m.DeclaringType==typeof(PatientTransfer)||m.DeclaringType==typeof(MinionStorage)),"pose blend creates or restores a second real identity");
        });
        Test("watcher visual binds simulation clock, policy and native anchor",()=>
        {
            var visual=typeof(ChamberProcess).Assembly.GetType("Baiye.Resurrection.Rescue.WatcherVisual");
            var sim=Calls(visual.GetMethod("Sim200ms"));var render=Calls(visual.GetMethod("RenderEveryTick"));
            Check(sim.Any(m=>m.DeclaringType.Name=="WatcherAnimation"&&m.Name=="Update"),"animation policy not wired");
            Check(sim.Any(m=>m.Name=="GetSymbolTransform")&&sim.Any(m=>m.Name=="Play"),"dock anchor or native clips not wired");
            Check(render.Any(m=>m.DeclaringType==typeof(GameClock)&&m.Name=="GetTime"),"interpolation ignores simulation clock");
            var apply=visual.GetMethod("ApplyPosition",BindingFlags.NonPublic|BindingFlags.Instance);
            Check(render.Any(m=>m==apply)&&sim.Any(m=>m==apply),"render or simulation skips interpolation reset");
            Check(Calls(apply).Any(m=>m.DeclaringType.Name=="WatcherMotionClock"&&m.Name=="Progress"),"native movement bypasses rewind-safe timing");
        });
        Test("watcher horizontal pose is opt-in for old chamber",()=>
        {
            var show=typeof(ChamberPatientVisual).GetMethod("Show");var args=show.GetParameters();
            Check(args.Length==6&&args[4].ParameterType==typeof(bool)&&args[4].HasDefaultValue&&(bool)args[4].DefaultValue==false&&args[5].HasDefaultValue&&(float)args[5].DefaultValue==1.5f,"existing chamber pose changed by default");
        });
        Test("bed and airborne patient use independent native appearance displays",()=>
        {
            var visual=typeof(ChamberProcess).Assembly.GetType("Baiye.Resurrection.Rescue.WatcherVisual");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Check(visual.GetField("bedDisplay",flags)?.FieldType==typeof(ChamberPatientVisual)&&visual.GetField("cargoDisplay",flags)?.FieldType==typeof(ChamberPatientVisual),"shared patient display would replace the occupied bed during another sortie");
            var render=Calls(visual.GetMethod("RenderEveryTick"));var cargo=Calls(visual.GetMethod("DrawCargo",flags));
            Check(render.Any(m=>m.DeclaringType==typeof(ChamberPatientVisual)&&m.Name=="ShowAnimated")&&cargo.Any(m=>m.DeclaringType==typeof(ChamberPatientVisual)&&m.Name=="ShowAnimated"),"bed and cargo displays are not independently animated");
            Check(!render.Any(m=>m.Name=="get_CorpseObject"),"parked corpse displayed on emergency bed");
        });
        Test("fallback triage and full cargo decisions are wired into production",()=>
        {
            var asm=typeof(ChamberProcess).Assembly;
            var station=asm.GetType("Baiye.Resurrection.Rescue.WatcherStation");
            var flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
            var select=Calls(station.GetMethod("FindFallbackTarget",flags));
            var tick=Calls(station.GetMethod("Sim200ms"));
            var escrow=Calls(station.GetMethod("ClassifyFallback",flags));
            Check(select.Any(m=>m.Name=="ClassifyFallback")&&tick.Any(m=>m.Name=="ClassifyFallback"),"fallback decision missing at selection or arrival");
            Check(escrow.Any(m=>m.Name=="Owns")&&escrow.Any(m=>m.Name=="ClassifyEscrow"),"fallback triage lacks verified storage ownership");
            Check(tick.Any(m=>m.Name=="DecideCargoAtHome")&&tick.Any(m=>m.Name=="TryBeginDelivery")&&tick.Any(m=>m.Name=="ReturnEmptyAfterCargoDelivery"),"full cargo recovery not connected to flight state machine");
            var marker=asm.GetType("Baiye.Resurrection.Rescue.WatcherCustody");
            Check(marker.GetField("Owner").GetCustomAttributes().Any(a=>a.GetType().Name=="Serialize"),"fallback owner is not saved");
        });
        Test("production state and patient participate in game save system",()=>
        {
            Check(typeof(ISaveLoadable).IsAssignableFrom(typeof(ChamberProcess)),"process not saved");
            Check(typeof(ISaveLoadable).IsAssignableFrom(typeof(PatientRecord)),"debt not saved");
            Check(typeof(StoredMinionIdentity.IStoredMinionExtension).IsAssignableFrom(typeof(PatientRecord)),"no native stasis extension");
            foreach(var f in new[]{"Model","Sequence","Selected","Requested","ReturnReady","PatientName"})Check(typeof(ChamberProcess).GetField(f).GetCustomAttributes().Any(a=>a.GetType().Name=="Serialize"),"missing field "+f);
            foreach(var f in new[]{"SuccessfulRevivals","Recovery"})Check(typeof(PatientRecord).GetField(f).GetCustomAttributes().Any(a=>a.GetType().Name=="Serialize"),"missing patient field "+f);
        });
        foreach(TreatmentStage stage in Enum.GetValues(typeof(TreatmentStage)))Test("native roundtrip stage "+stage,()=>
        {
            var e=new Envelope{Treatment=new TreatmentModel{Stage=stage,RebuildTime=1234.5,RecoveryTime=2345.25,NutritionPaid=35800,PriorRevivals=2,DoctorRemaining=17.75,Revision=21},Debt=new RecoveryDebt{Active=true,PriorRevivals=2,RecoveryTime=1200,NutritionPaid=48000},Requested=true,PatientName="米玛-原身份",Revivals=3};
            var r=RoundTrip(e);
            Check(r.Treatment.Stage==stage&&r.Treatment.RebuildTime==1234.5&&r.Treatment.RecoveryTime==2345.25&&r.Treatment.NutritionPaid==35800,"progress changed");
            Check(r.Treatment.PriorRevivals==2&&r.Treatment.DoctorRemaining==17.75&&r.Treatment.Revision==21,"doctor/repeat state lost");
            Check(r.Debt.Active&&r.Debt.RecoveryTime==1200&&r.Debt.NutritionPaid==48000&&r.Debt.PriorRevivals==2,"debt lost");
            Check(r.Requested&&r.PatientName==e.PatientName&&r.Revivals==3,"identity name/request lost");
        });
        Test("patient transfer extension deep copies actual debt",()=>
        {
            var a=(PatientRecord)RuntimeHelpers.GetUninitializedObject(typeof(PatientRecord));
            var b=(PatientRecord)RuntimeHelpers.GetUninitializedObject(typeof(PatientRecord));
            a.SuccessfulRevivals=3;a.Recovery=new RecoveryDebt{Active=true,PriorRevivals=2,RecoveryTime=1450,NutritionPaid=58000};
            PatientRecord.CopyData(a,b);Check(b.SuccessfulRevivals==3&&b.Recovery.RecoveryTime==1450&&b.Recovery.NutritionPaid==58000,"extension changed debt");
            a.Recovery.RecoveryTime=0;Check(b.Recovery.RecoveryTime==1450,"debt alias shared across identities");
        });
        Test("native storage envelope retains sole patient name and reference field",()=>
        {
            var info=new MinionStorage.Info("哈奇原名",new Ref<KPrefabID>());
            var storage=(MinionStorage)RuntimeHelpers.GetUninitializedObject(typeof(MinionStorage));
            storage.SetStoredMinionInfo(new List<MinionStorage.Info>{info});
            Check(storage.GetStoredMinionInfo().Count==1&&storage.GetStoredMinionInfo()[0].id==info.id,"native patient identity not stable");
            // Guid internals differ between the host CLR and Unity Mono. A
            // chamber has exactly one native Info entry, so release looks up
            // that entry's own current Guid rather than an independently saved
            // Guid. Check name/reference fields here; do not claim a game save.
            var r=RoundTrip(new StorageEnvelope{Info=info});Check(r.Info.name==info.name&&r.Info.serializedMinion!=null,"native patient name/reference field lost");
        });
        Test("doctor adapter is interruptible and checks ongoing native work schedule",()=>
        {
            Check(typeof(Workable).IsAssignableFrom(typeof(ChamberDoctor)),"no native workable");
            var calls=Calls(typeof(ChamberDoctor).GetMethod("Sim200ms"));
            Check(calls.Any(m=>m.DeclaringType==typeof(Schedulable)&&m.Name=="IsAllowed"),"only initial eligibility enforced");
            Check(calls.Any(m=>m.DeclaringType==typeof(ChamberDoctor)&&m.Name=="Cancel"),"no sleep interruption");
            var stop=Calls(typeof(ChamberDoctor).GetMethod("OnStopWork",BindingFlags.NonPublic|BindingFlags.Instance));
            Check(stop.Any(m=>m.DeclaringType==typeof(Workable)&&m.Name=="get_WorkTimeRemaining"),"work interruption does not preserve progress");
            var ctor=Calls(typeof(WorkChore<ChamberDoctor>).GetConstructors().Single());
            Check(ctor.Any(m=>m.Name=="AddPrecondition"),"native work requirements missing");
        });
        Test("production identity release uses native restore, not rerolled stats",()=>
        {
            var calls=Calls(typeof(PatientTransfer).GetMethod("Release"));
            Check(calls.Any(m=>m.DeclaringType==typeof(MinionStorage)&&m.Name=="DeserializeMinion"),"no native identity restore");
            Check(!calls.Any(m=>m.DeclaringType==typeof(MinionStartingStats)),"random replacement generated");
            var entry=Calls(typeof(PatientTransfer).GetMethod("EnterStasis"));
            Check(entry.Any(m=>m.DeclaringType==typeof(MinionStorage)&&m.Name=="SerializeMinion"),"not preserving original profile");
        });
        Test("native restore API retains the resume and cultivated attributes",()=>
        {
            var method=typeof(MinionStorage).GetMethods(BindingFlags.NonPublic|BindingFlags.Static).Single(m=>m.Name=="CopyMinion"&&m.GetParameters()[0].ParameterType==typeof(StoredMinionIdentity));
            var calls=Calls(method);
            Check(calls.Any(m=>m.DeclaringType==typeof(MinionResume)&&m.Name=="RestoreResume"),"resume contract changed");
            Check(calls.Any(m=>m.DeclaringType==typeof(AttributeLevels)&&m.Name=="OnDeserialized"),"learned attributes contract changed");
            Check(calls.Any(m=>m.DeclaringType==typeof(ChoreConsumer)&&m.Name=="SetChoreGroupPriorities"),"priorities contract changed");
            Check(calls.Any(m=>m.DeclaringType==typeof(MinionIdentity)&&m.Name=="SetName"),"name contract changed");
        });
        Test("saved native control choices survive prefab clone",()=>
        {
            Check(typeof(ISidescreenButtonControl).IsAssignableFrom(typeof(ChamberControl)),"no native controls");
            Check(typeof(ChamberControl).GetField("Choice").GetCustomAttribute<UnityEngine.SerializeField>()!=null,"choice not cloned");
            Check(ChamberControl.Count==3&&ChamberConfig.StorageLimits.Length==6,"save layout changed");
        });
        Test("database extension idempotent for both languages",()=>
        {
            foreach(bool zh in new[]{true,false})
            {
                var entry=new CodexEntry("OriginalEntry",new List<ContentContainer>(),"OriginalTitle");
                var one=ChamberCodex.Append(entry,ChamberStrings.ID,zh);int count=one.contentContainers.Count;
                Check(count==6,"guide missing sections");Check(object.ReferenceEquals(one,ChamberCodex.Append(one,ChamberStrings.ID,zh)),"entry replaced");Check(one.contentContainers.Count==count,"guide duplicated");
                Check(object.ReferenceEquals(entry,ChamberCodex.Append(entry,"OtherBuilding",zh)),"unrelated guide modified");
            }
        });
        Test("rendering clothing containers cannot alias patient save",()=>
        {
            var original=new Dictionary<ClothingOutfitUtility.OutfitType,List<ResourceRef<Database.ClothingItemResource>>>{{ClothingOutfitUtility.OutfitType.Clothing,new List<ResourceRef<Database.ClothingItemResource>>()}};
            var copy=ChamberPatientVisual.CopyClothing(original);
            Check(!ReferenceEquals(original,copy)&&!ReferenceEquals(original[ClothingOutfitUtility.OutfitType.Clothing],copy[ClothingOutfitUtility.OutfitType.Clothing]),"clothing aliases patient save");
            copy.Clear();Check(original.Count==1,"rendering mutated patient outfit");
            Check(ChamberPatientVisual.CopyAccessories(null).Count==0&&ChamberPatientVisual.CopyWearables(null).Count==0,"missing appearance crashes");
        });
        Test("nested native Wearable is cloned before restoration",()=>
        {
            var w=new WearableAccessorizer.Wearable(new List<KAnimFile>(),4);
            var original=new Dictionary<WearableAccessorizer.WearableType,WearableAccessorizer.Wearable>{{WearableAccessorizer.WearableType.CustomClothing,w}};
            var copy=ChamberPatientVisual.CopyWearables(original);var c=copy[WearableAccessorizer.WearableType.CustomClothing];
            Check(!ReferenceEquals(c,w)&&!ReferenceEquals(c.AnimNames,w.AnimNames)&&!ReferenceEquals(c.BuildAnims,w.BuildAnims),"wearable aliases saved data");
            Check(c.buildOverridePriority==4,"clothing layer priority lost");c.AnimNames.Add("test-only");Check(w.AnimNames.Count==0,"render changed stored wearable");
        });
        Test("appearance adapter uses native accessories and wearables",()=>
        {
            var calls=Calls(typeof(ChamberPatientVisual).GetMethod("ApplyAppearance"));
            Check(calls.Any(m=>m.DeclaringType==typeof(Accessorizer)&&m.Name=="ApplyAccessories"),"patient hair/skin not applied");
            Check(calls.Any(m=>m.DeclaringType==typeof(WearableAccessorizer)&&m.Name=="RestoreWearables"),"patient clothes not applied");
            Check(calls.Any(m=>m.DeclaringType==typeof(BaseMinionConfig)&&m.Name=="CopyVisibleSymbols"),"corpse symbol visibility lost");
            Check(!calls.Any(m=>m.DeclaringType==typeof(MinionStorage)||m.DeclaringType==typeof(MinionStartingStats)),"rendering changes patient identity");
            // KMonoBehaviour itself implements ISaveLoadable. Display ownership
            // is opt-in and no rendering reference participates in the save.
            Check(typeof(ChamberPatientVisual).GetCustomAttribute<SerializationConfig>()!=null,"display save policy missing");
            Check(!typeof(ChamberPatientVisual).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Any(f=>f.GetCustomAttributes().Any(a=>a.GetType().Name=="Serialize")),"runtime display reference saved");
        });
        foreach(PodPhase phase in Enum.GetValues(typeof(PodPhase)))Test("native door/fluid/carry save roundtrip "+phase,()=>
        {
            var e=new Envelope{Sequence=new ChamberSequence{Version=1,Phase=phase,Door=.4,Fluid=.65}};
            var r=RoundTrip(e);Check(r.Sequence.Version==1&&r.Sequence.Phase==phase&&r.Sequence.Door==.4&&r.Sequence.Fluid==.65,"door/fluid state changed");
        });
        Test("doctor completes drain authorization without deserializing patient",()=>
        {
            var calls=Calls(typeof(ChamberProcess).GetMethod("DoctorFinished"));
            Check(!calls.Any(m=>m.Name=="Release"||m.Name=="ReleaseAt"||m.Name=="DeserializeMinion"),"doctor bypasses carry");
            Check(calls.Any(m=>m.DeclaringType==typeof(ChamberSequence)&&m.Name=="FinishExam"),"no saved discharge authorization");
            var finish=Calls(typeof(ChamberProcess).GetMethod("FinishCarry"));
            Check(finish.Any(m=>m.DeclaringType==typeof(PatientTransfer)&&m.Name=="IsSafeCell"),"no safe deposit check");
            Check(finish.Any(m=>m.Name=="ReleaseAt"),"carrier cannot restore original");
        });
        Test("carry cleanup retains stasis and clears carrier animation override",()=>
        {
            var calls=Calls(typeof(ChamberCarryChore).GetMethod("End",BindingFlags.Instance|BindingFlags.NonPublic));
            Check(calls.Any(m=>m.Name=="SetOverride")&&calls.Any(m=>m.Name=="InterruptCarry"),"carry interruption not cleaned");
            Check(!calls.Any(m=>m.Name=="Release"||m.Name=="DeserializeMinion"),"interruption releases patient unsafely");
            var carry=typeof(ChamberCarryChore).GetMethod("SetOverride",BindingFlags.Instance|BindingFlags.NonPublic);
            Check(Calls(carry).Any(m=>m.Name=="RemoveAnimOverrides"),"carrier animation leaked");
        });
        Test("patient pose computes native symbol geometry and follows carrier each frame",()=>
        {
            var calls=Calls(typeof(ChamberPatientVisual).GetMethod("FitPatientPose",BindingFlags.NonPublic|BindingFlags.Instance));
            Check(calls.Any(m=>m.Name=="TryGetFrame")&&calls.Any(m=>m.Name=="GetFrame"),"medical pose ignores body bounds");
            Check(typeof(IRenderEveryTick).IsAssignableFrom(typeof(ChamberPatientVisual)),"carry display only updates 5 Hz");
        });
        Test("pod and carry select distinct validated native poses",()=>
        {
            Check(ChamberPatientVisual.PodPose=="idle_default","pod uses a bed pose or missing idle_loop");
            Check(ChamberPatientVisual.CarryPose=="working_loop","carried pose changed");
            var select=Calls(typeof(ChamberPatientVisual).GetMethod("SelectPose",BindingFlags.NonPublic|BindingFlags.Instance));
            Check(select.Any(m=>m.Name=="FitPatientPose")&&select.Any(m=>m.Name=="GetAnim"),"pose switch not fitted to selected native animation");
        });
        Test("nutrition shortage actively requests native delivery beyond the one kg refill threshold",()=>
        {
            var calls=Calls(typeof(ChamberProcess).GetMethod("UpdateDeliveries",BindingFlags.NonPublic|BindingFlags.Instance));
            Check(calls.Any(m=>m.DeclaringType==typeof(ManualDeliveryKG)&&m.Name=="RequestDelivery"),"nutrition shortage only unpauses delivery; native one kg threshold still blocks refill");
        });
        Test("invalid stored food leaves physically without destruction or nutrient credit",()=>
        {
            var calls=Calls(typeof(ChamberProcess).GetMethod("ClearInvalidFood",BindingFlags.NonPublic|BindingFlags.Instance));
            Check(calls.Any(m=>m.DeclaringType==typeof(Storage)&&m.Name=="Drop"),"invalid food stays inside");
            Check(!calls.Any(m=>m.Name=="KDestroyGameObject"||m.Name=="Commit"||m.Name=="ConsumeIgnoringDisease"),"discard destroys material or credits treatment");
            var usable=Calls(typeof(ChamberProcess).GetMethod("UsableFood",BindingFlags.NonPublic|BindingFlags.Static));
            Check(usable.Any(m=>m.DeclaringType==typeof(Rottable.Instance)&&m.Name=="get_RotValue"),"expired edible still counts before native rot replacement");
        });
        Test("material adapter binds native task state and functional delivery APIs",()=>
        {
            var adapter=typeof(ChamberProcess).Assembly.GetType("Baiye.Resurrection.NativeMaterialDelivery",true);
            var state=Calls(adapter.GetProperty("HasTask").GetMethod);
            Check(state.Any(m=>m.DeclaringType==typeof(ManualDeliveryKG)&&m.Name=="get_DebugFetchList")&&state.Any(m=>m.DeclaringType==typeof(FetchList2)&&m.Name=="get_IsComplete"),"active native fetch state unavailable");
            foreach(var name in new[]{"Pause","UpdateDeliveryState"})
                Check(Calls(adapter.GetMethod(name)).Any(m=>m.DeclaringType==typeof(ManualDeliveryKG)&&m.Name==name),"native delivery API missing: "+name);
        });
        Test("material adapter requests and aborts through actual native API signatures",()=>
        {
            var adapter=typeof(ChamberProcess).Assembly.GetType("Baiye.Resurrection.NativeMaterialDelivery",true);
            var request=Calls(adapter.GetMethod("RequestDelivery"));var abort=Calls(adapter.GetMethod("AbortDelivery"));
            Check(request.Any(m=>m.DeclaringType==typeof(ManualDeliveryKG)&&m.Name=="RequestDelivery"&&m.GetParameters().Length==0),"native request binding missing");
            Check(abort.Any(m=>m.DeclaringType==typeof(ManualDeliveryKG)&&m.Name=="AbortDelivery"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(string)),"native abort binding missing");
        });
        Console.WriteLine(passed+" native managed contract scenarios passed (Unity world not started)");
    }
    [SerializationConfig(MemberSerialization.OptIn)]
    public class StorageEnvelope { [Serialize] public MinionStorage.Info Info; }
    static readonly Dictionary<short,OpCode> OpCodesByValue=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
    static List<MethodBase> Calls(MethodBase method)
    {
        var result=new List<MethodBase>();var bytes=method.GetMethodBody()?.GetILAsByteArray();if(bytes==null)return result;
        for(int i=0;i<bytes.Length;)
        {
            short key=bytes[i++];if(key==0xfe)key=(short)(0xfe00|bytes[i++]);var op=OpCodesByValue[key];int n=0;
            switch(op.OperandType)
            {
                case OperandType.InlineMethod:
                    try{result.Add(method.Module.ResolveMethod(BitConverter.ToInt32(bytes,i),method.DeclaringType?.GetGenericArguments(),method.IsGenericMethod?method.GetGenericArguments():null));}catch(ArgumentException){} n=4;break;
                case OperandType.InlineSwitch:n=4+4*BitConverter.ToInt32(bytes,i);break;
                case OperandType.InlineI8:case OperandType.InlineR:n=8;break;
                case OperandType.InlineVar:n=2;break;
                case OperandType.ShortInlineVar:case OperandType.ShortInlineI:case OperandType.ShortInlineBrTarget:n=1;break;
                case OperandType.InlineNone:n=0;break;
                default:n=4;break;
            }
            i+=n;
        }
        return result;
    }
}
