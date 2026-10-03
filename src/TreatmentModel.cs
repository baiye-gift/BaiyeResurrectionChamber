using System;
#if GAME_HOST
using KSerialization;
#endif
namespace Baiye.Resurrection
{
    public enum TreatmentStage { Empty, StartCheck, Rebuilding, RecoveryCheck, Recovering, DischargeCheck }
#if GAME_HOST
    [SerializationConfig(MemberSerialization.OptIn)]
#endif
    public sealed class TreatmentModel
    {
#if GAME_HOST
        [Serialize]
#endif
        public TreatmentStage Stage;
#if GAME_HOST
        [Serialize]
#endif
        public double RebuildTime;
#if GAME_HOST
        [Serialize]
#endif
        public double RecoveryTime;
#if GAME_HOST
        [Serialize]
#endif
        public double NutritionPaid;
#if GAME_HOST
        [Serialize]
#endif
        public int PriorRevivals;
#if GAME_HOST
        [Serialize]
#endif
        public double DoctorRemaining = 60;
#if GAME_HOST
        [Serialize]
#endif
        public int Revision;
        public const double RebuildDuration = 1800, RecoveryDuration = 3000;
        public double Multiplier => 1 + .5 * Math.Max(0, PriorRevivals);
        public double NutritionTarget => 60000 * Multiplier;
        public bool NeedsDoctor => Stage == TreatmentStage.StartCheck || Stage == TreatmentStage.RecoveryCheck || Stage == TreatmentStage.DischargeCheck;
        public bool IsAutomatic => Stage == TreatmentStage.Rebuilding || Stage == TreatmentStage.Recovering;
        public void Admit(int revivals)
        {
            if(Stage != TreatmentStage.Empty) throw new InvalidOperationException("Chamber occupied");
            PriorRevivals = Math.Max(0, revivals); RebuildTime = RecoveryTime = NutritionPaid = 0;
            Enter(TreatmentStage.StartCheck);
        }
        public void Resume(RecoveryDebt debt)
        {
            if(Stage != TreatmentStage.Empty || debt == null || !debt.Active) throw new InvalidOperationException("Invalid recovery admission");
            PriorRevivals = debt.PriorRevivals; RebuildTime = RebuildDuration;
            RecoveryTime = Math.Max(0, Math.Min(RecoveryDuration, debt.RecoveryTime));
            NutritionPaid = Math.Max(0, Math.Min(NutritionTarget, debt.NutritionPaid));
            Enter(ReadyForDischarge ? TreatmentStage.DischargeCheck : TreatmentStage.RecoveryCheck);
        }
        public bool CompleteDoctorCheck()
        {
            if(!NeedsDoctor || DoctorRemaining > 1e-8) return false;
            if(Stage == TreatmentStage.StartCheck) Enter(TreatmentStage.Rebuilding);
            else if(Stage == TreatmentStage.RecoveryCheck) Enter(TreatmentStage.Recovering);
            else Enter(TreatmentStage.Empty);
            return true;
        }
        // Saved work progress survives a different doctor and a save/load.
        public bool DoctorWork(double seconds)
        {
            if(!NeedsDoctor || !FinitePositive(seconds)) return false;
            DoctorRemaining = Math.Max(0, DoctorRemaining - seconds);
            return DoctorRemaining <= 1e-8;
        }
        bool ReadyForDischarge => RecoveryTime >= RecoveryDuration - 1e-7 && NutritionPaid >= NutritionTarget - 1e-7;
        public Bill Preview(double dt)
        {
            if(!IsAutomatic || !FinitePositive(dt)) return default;
            double seconds = Math.Min(dt, Stage == TreatmentStage.Rebuilding ? RebuildDuration - RebuildTime : RecoveryDuration - RecoveryTime);
            if(seconds <= 1e-9) return default;
            var b = new Bill { Stage = Stage, Revision = Revision, Seconds = seconds };
            if(Stage == TreatmentStage.Rebuilding)
            {
                b.Metal = 100 * Multiplier * seconds / RebuildDuration;
                b.Plastic = 50 * Multiplier * seconds / RebuildDuration;
                b.Water = 200 * seconds / RebuildDuration; b.Oxygen = 40 * seconds / RebuildDuration;
            }
            else
            {
                b.Water = 300 * seconds / RecoveryDuration; b.Oxygen = 60 * seconds / RecoveryDuration;
                b.Nutrition = Math.Min(Math.Max(0, NutritionTarget - NutritionPaid), NutritionTarget * seconds / RecoveryDuration);
            }
            return b;
        }
        public Bill Payable(double dt, TreatmentSupplies supplies, bool paid)
        {
            if(!paid || supplies==null)return default;
            var bill=Preview(dt);return supplies.Covers(bill)?bill:default;
        }
        public void Commit(Bill bill)
        {
            var expected = Preview(bill.Seconds);
            if(expected.Seconds == 0 || bill.Revision != Revision || bill.Stage != Stage || !bill.SameAmounts(expected))
                throw new InvalidOperationException("Stale or invalid treatment bill");
            if(Stage == TreatmentStage.Rebuilding)
            {
                RebuildTime = Math.Min(RebuildDuration, RebuildTime + bill.Seconds);
                if(RebuildTime >= RebuildDuration - 1e-7) Enter(TreatmentStage.RecoveryCheck);
            }
            else
            {
                RecoveryTime = Math.Min(RecoveryDuration, RecoveryTime + bill.Seconds);
                NutritionPaid = Math.Min(NutritionTarget, NutritionPaid + bill.Nutrition);
                if(ReadyForDischarge) Enter(TreatmentStage.DischargeCheck);
            }
            Revision++;
        }
        public void LifeSupportLost(double dt)
        {
            if(Stage != TreatmentStage.Recovering && Stage != TreatmentStage.DischargeCheck) return;
            if(!FinitePositive(dt)) return;
            RecoveryTime = Math.Max(0, RecoveryTime - dt * .1);
            if(Stage == TreatmentStage.DischargeCheck) Enter(TreatmentStage.Recovering);
            Revision++;
        }
        public RecoveryDebt Debt() => new RecoveryDebt { Active = RebuildTime >= RebuildDuration - 1e-7, PriorRevivals = PriorRevivals, RecoveryTime = RecoveryTime, NutritionPaid = NutritionPaid };
        public void Reset() { RebuildTime = RecoveryTime = NutritionPaid = 0; PriorRevivals = 0; Enter(TreatmentStage.Empty); }
        void Enter(TreatmentStage stage) { Stage = stage; DoctorRemaining = 60; Revision++; }
        static bool FinitePositive(double n) => n > 0 && !double.IsNaN(n) && !double.IsInfinity(n);
    }
    public struct Bill
    {
        public TreatmentStage Stage;
        public int Revision;
        public double Seconds, Metal, Plastic, Water, Oxygen, Nutrition;
        public bool SameAmounts(Bill b) => Near(Seconds,b.Seconds) && Near(Metal,b.Metal) && Near(Plastic,b.Plastic) && Near(Water,b.Water) && Near(Oxygen,b.Oxygen) && Near(Nutrition,b.Nutrition);
        static bool Near(double a, double b) => Math.Abs(a-b) < 1e-7;
    }
    public sealed class TreatmentSupplies
    {
        public double Metal,Plastic,Water,Oxygen,Nutrition;
        public bool Covers(Bill b) => Metal+1e-7>=b.Metal && Plastic+1e-7>=b.Plastic && Water+1e-7>=b.Water && Oxygen+1e-7>=b.Oxygen && Nutrition+1e-7>=b.Nutrition;
    }
#if GAME_HOST
    [SerializationConfig(MemberSerialization.OptIn)]
#endif
    public sealed class RecoveryDebt
    {
#if GAME_HOST
        [Serialize]
#endif
        public bool Active;
#if GAME_HOST
        [Serialize]
#endif
        public int PriorRevivals;
#if GAME_HOST
        [Serialize]
#endif
        public double RecoveryTime;
#if GAME_HOST
        [Serialize]
#endif
        public double NutritionPaid;
    }
}
