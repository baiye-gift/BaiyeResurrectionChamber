using System;
#if GAME_HOST
using KSerialization;
#endif
namespace Baiye.Resurrection
{
    public enum PodPhase { Empty, Closing, Sealed, Opening, AwaitingCarrier, Carrying }
#if GAME_HOST
    [SerializationConfig(MemberSerialization.OptIn)]
#endif
    public sealed class ChamberSequence
    {
#if GAME_HOST
        [Serialize]
#endif
        public int Version;
#if GAME_HOST
        [Serialize]
#endif
        public PodPhase Phase;
#if GAME_HOST
        [Serialize]
#endif
        public double Door;
#if GAME_HOST
        [Serialize]
#endif
        public double Fluid;
        public bool ExamAllowed=>Phase==PodPhase.Sealed&&Door>=1;
        public bool TreatmentAllowed=>ExamAllowed&&Fluid>=1;
        public bool DischargeAuthorized=>Phase==PodPhase.Opening||Phase==PodPhase.AwaitingCarrier||Phase==PodPhase.Carrying;
        public bool CarrierEligible(bool alreadyAssigned)=>Phase==PodPhase.AwaitingCarrier||(Phase==PodPhase.Carrying&&alreadyAssigned);
        public void Restore(TreatmentModel model)
        {
            // Old saves lack this object. Preserve their treatment/doctor work.
            if(Version==0)
            {
                Reset();
                if(model.Stage!=TreatmentStage.Empty)
                {
                    Phase=PodPhase.Sealed;Door=1;Fluid=1;
                    if(model.Stage==TreatmentStage.StartCheck)Fluid=FillFor(model.DoctorRemaining);
                    if(model.Stage==TreatmentStage.DischargeCheck)Fluid=DrainFor(model.DoctorRemaining);
                }
            }
            // Carrying is a transient chore; identity remains in native stasis.
            // On load re-offer the carry task with the chamber open and empty.
            if(Phase==PodPhase.Carrying)Phase=PodPhase.AwaitingCarrier;
        }
        public void Admit(){Version=1;Phase=PodPhase.Closing;Door=Fluid=0;}
        public void Tick(double dt,bool powered)
        {
            if(!powered||dt<=0||double.IsNaN(dt)||double.IsInfinity(dt))return;
            if(Phase==PodPhase.Closing){Door=Math.Min(1,Door+dt/2);if(Door>=1)Phase=PodPhase.Sealed;}
            if(Phase==PodPhase.Opening){Door=Math.Max(0,Door-dt/2);if(Door<=0)Phase=PodPhase.AwaitingCarrier;}
        }
        public void DoctorProgress(TreatmentStage stage,double remaining)
        {
            if(!ExamAllowed)return;
            if(stage==TreatmentStage.StartCheck||stage==TreatmentStage.RecoveryCheck)
                Fluid=Math.Max(Fluid,FillFor(remaining));
            else if(stage==TreatmentStage.DischargeCheck)Fluid=Math.Min(Fluid,DrainFor(remaining));
        }
        public bool FinishExam(TreatmentStage stage,double remaining)
        {
            if(!ExamAllowed||remaining>1e-8)return false;
            DoctorProgress(stage,0);
            if(stage==TreatmentStage.DischargeCheck){Fluid=0;Phase=PodPhase.Opening;}
            return true;
        }
        public void RefillForRecovery(){if(!DischargeAuthorized){Phase=PodPhase.Sealed;Door=Fluid=1;}}
        public bool BeginCarry(){if(Phase!=PodPhase.AwaitingCarrier||Door!=0||Fluid!=0)return false;Phase=PodPhase.Carrying;return true;}
        public void InterruptCarry(){if(Phase==PodPhase.Carrying)Phase=PodPhase.AwaitingCarrier;}
        public void Reset(){Version=1;Phase=PodPhase.Empty;Door=Fluid=0;}
        static double FillFor(double remaining)=>Math.Max(0,Math.Min(1,(20-remaining)/20));
        static double DrainFor(double remaining)=>Math.Max(0,Math.Min(1,remaining/20));
    }
    public static class SupplyRequirements
    {
        public static TreatmentSupplies Startup(TreatmentModel model)=>new TreatmentSupplies{Metal=100*model.Multiplier,Plastic=50*model.Multiplier,Water=60,Oxygen=12,Nutrition=12000*model.Multiplier};
        public static TreatmentSupplies Slice(Bill bill)=>new TreatmentSupplies{Metal=bill.Metal,Plastic=bill.Plastic,Water=bill.Water,Oxygen=bill.Oxygen,Nutrition=bill.Nutrition};
        public static string Missing(TreatmentSupplies have,TreatmentSupplies need,bool chinese)
        {
            var names=new System.Collections.Generic.List<string>();
            if(have.Oxygen+1e-7<need.Oxygen)names.Add(chinese?"氧气":"oxygen");
            if(have.Water+1e-7<need.Water)names.Add(chinese?"水":"water");
            if(have.Metal+1e-7<need.Metal)names.Add(chinese?"精炼金属":"refined metal");
            if(have.Plastic+1e-7<need.Plastic)names.Add(chinese?"塑料":"plastic");
            if(have.Nutrition+1e-7<need.Nutrition)names.Add(chinese?"品质 ≥ +2 营养食品":"quality ≥ +2 food");
            return names.Count==0?"":(chinese?"缺少：":"Missing: ")+string.Join(chinese?"、":", ",names);
        }
    }
}
