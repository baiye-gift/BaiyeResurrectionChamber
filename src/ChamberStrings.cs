using System.Globalization;
namespace Baiye.Resurrection
{
    public static class ChamberStrings
    {
        public const string ID = "BaiyeLifeReconstructionChamber";
        public const string FoodTag = "BaiyeRecoveryFood", WeakEffect = "BaiyeRecoveryWeakness", SevereEffect = "BaiyeRecoveryDebt";
        public static readonly HashedString AttentionPort = "BaiyeMedicalAttention";
        public static bool Chinese => Localization.GetLocale()?.Code.StartsWith("zh") ?? CultureInfo.CurrentUICulture.Name.StartsWith("zh");
        public static string T(string zh, string en) => Chinese ? zh : en;
        public static string StageName(TreatmentStage stage)
        {
            switch(stage)
            {
                case TreatmentStage.StartCheck:return T("等待启动检查","Awaiting startup examination");
                case TreatmentStage.Rebuilding:return T("组织重建","Tissue reconstruction");
                case TreatmentStage.RecoveryCheck:return T("等待康复检查","Awaiting recovery examination");
                case TreatmentStage.Recovering:return T("仓内康复","Assisted recovery");
                case TreatmentStage.DischargeCheck:return T("等待出仓检查","Awaiting discharge examination");
                default:return T("空仓","Empty");
            }
        }
        public static void Register()
        {
            string key = "STRINGS.BUILDINGS.PREFABS." + ID.ToUpperInvariant();
            Strings.Add(key+".NAME",T("生命重建仓","Life Reconstruction Chamber"));
            Strings.Add(key+".DESC",T("付出医疗资源与时间，挽救值得留下的伙伴。","Recover a valued colonist through costly, reliable medical treatment."));
            Strings.Add(key+".EFFECT",T("搬入仍存在的普通复制人遗体，重建 3 周期、仓内康复至少 5 周期。启动储备：100 kg 精炼金属、50 kg 塑料、60 kg 水、12 kg 氧气、12000 kcal 品质 ≥ +2 食品；金属/塑料/营养随复活次数加价。氧气必须接气体输入口，环境空气不计入。医生在启动、转入康复、出仓时各检查 60 秒，遵守作息；检查末段填充/回收仓内治疗液，自动治疗无需驻守。正常出仓先由医生排液开门，再由复制人搬到安全地面。无需排液口。已安葬、遗体被销毁及仿生复制人不支持。","Deliver an existing standard duplicant corpse. Reconstruction takes 3 cycles; recovery at least 5. Startup stock: 100 kg refined metal, 50 kg plastic, 60 kg water, 12 kg oxygen, 12000 kcal quality ≥ +2 food. Metal/plastic/nutrition scale with previous revivals. Oxygen must enter through the gas input; surrounding air does not count. Doctors perform three scheduled 60-second checks; their final portion fills or drains internal treatment fluid. Automatic treatment needs no attending doctor. Discharge requires doctor drainage and door opening, then a duplicant carries the patient to safe floor. No liquid output required. Buried, destroyed and bionic bodies unsupported."));
        }
    }
}
