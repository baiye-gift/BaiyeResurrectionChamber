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
