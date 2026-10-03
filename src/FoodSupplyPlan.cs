using System;
namespace Baiye.Resurrection
{
    // Heat determines demand; mass is only the native transport/storage unit.
    public struct FoodSupplyPlan
    {
        public double TargetKcal,MissingKcal,CapacityKg;
        public bool NeedsFood,CanRequest;
        public static double ReserveTarget(TreatmentModel model,bool occupied,bool dischargeAuthorized)
        {
            if(!occupied||dischargeAuthorized||model.Stage==TreatmentStage.DischargeCheck)return 0;
            double reserve=12000*model.Multiplier;
            if(model.Stage==TreatmentStage.RecoveryCheck||model.Stage==TreatmentStage.Recovering)
                reserve=Math.Min(reserve,Math.Max(0,model.NutritionTarget-model.NutritionPaid));
            return reserve;
        }
        public static FoodSupplyPlan Create(double target,double kcal,double taggedMass,double totalMass,double capacity,double kcalPerKg)
        {
            var p=new FoodSupplyPlan{TargetKcal=Nonnegative(target),CapacityKg=Nonnegative(taggedMass)};
            p.MissingKcal=Math.Max(0,p.TargetKcal-Nonnegative(kcal));
            p.NeedsFood=p.MissingKcal>1e-7;
            double room=Math.Max(0,Nonnegative(capacity)-Nonnegative(totalMass));
            p.CanRequest=p.NeedsFood&&room>.001;
            if(p.CanRequest)
            {
                // No known source yet: offer a small task, recalculated when food arrives.
                double amount=Positive(kcalPerKg)?p.MissingKcal/kcalPerKg:1;
                p.CapacityKg+=Math.Min(room,Math.Min(200,Math.Max(.01,amount)));
            }
            return p;
        }
        static bool Positive(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n)&&n>0;
        static double Nonnegative(double n)=>Positive(n)?n:0;
    }
}
