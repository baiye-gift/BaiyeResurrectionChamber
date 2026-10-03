using System.Collections.Generic;
using System.Linq;
namespace Baiye.Resurrection
{
    public static class ChamberCodex
    {
        public static void Attach(BuildingDef def)
        {
            var previous=def.ExtendCodexEntry;
            def.ExtendCodexEntry=entry=>Append(previous!=null?previous(entry):entry,def.PrefabID,ChamberStrings.Chinese);
        }
        public static CodexEntry Append(CodexEntry entry,string id,bool zh)
        {
            if(entry==null||id!=ChamberStrings.ID)return entry;
            const string marker="BAIYE_RECONSTRUCTION_GUIDE";
            if(entry.contentContainers.Any(c=>c?.content?.OfType<CodexText>().Any(t=>t.messageID==marker)==true))return entry;
            var sections=Sections(zh);
            for(int i=0;i<sections.Length;i+=2)entry.AddContentContainer(new ContentContainer(new List<ICodexWidget>{new CodexText(sections[i],CodexTextStyle.Subtitle,i==0?marker:null),new CodexText(sections[i+1])},ContentContainer.ContentLayout.Vertical));
            return entry;
        }
        public static string[] Sections(bool zh)=>zh?new[]{
            "建设与连接","4×4 地面建筑，医疗菜单，MedicineIV 医疗研究；800 kg 精炼金属、200 kg 塑料建造。底部左 (-1,0) 水输入、右 (1,0) 氧气输入、中 (0,0) 电力。第二排左 (-1,1) 自动化运行输入、右 (1,1) 医疗请求输出。医院房间推荐，不强制。两侧各留一格医生/病人站位和至少两格头部空间。",
            "选择与遗体","配置页循环选择本世界普通复制人遗体，然后安排入仓。每仓一人，标准搬运任务，可设置建筑优先级。安装前遗体仍存在者也可接收。已送入原版墓碑的对象已销毁，无法恢复。首版不处理仿生复制人。原身份、技能、特质、兴趣、经验、属性等级、外观、日程、优先级及饮食限制通过原生保存恢复；床位/装备分配需重设。",
            "治疗费用","首次重建 3 周期（1800 s）：100 kg 精炼金属、50 kg 塑料、200 kg 水、40 kg 氧，2400 W。仓内康复至少 5 周期（3000 s）：300 kg 水、60 kg 氧、60000 kcal 品质 >= +2 食品，1200 W。食品按实际热量消费，仓内仍可腐败，腐败不计营养；水氧为游戏治疗工艺净消耗。每人重建金属/塑料与营养倍数 = 1 + 0.5 × 以前成功复活次数；水/氧/时间固定。启动需全部重建金属/塑料及至少 60 kg 水、12 kg 氧、12000×倍数 kcal。",
            "医生与作息","医生启动重建、转入康复、检查出仓，每次基础工作 60 s，医生速度影响检查效率。需要原版医疗技能 CanDoctor。医生遵守工作、吃饭、睡眠安排；不抢占睡眠。中途离岗、换班和读档保留已完成检查秒数及液面。遗体送达先关门，启动检查末 20 基础秒逐渐填液，出仓检查末 20 基础秒回收液体；无需排液口。医生不连续驻守，八周期自动治疗在医生睡觉时继续；到下一检查节点等待医生，不推进、不扣治疗材料。",
            "康复与紧急释放","重建完成后进入医疗休眠，康复阶段保留原身份，不参加基地工作。有效康复时间与营养全部满足，经医生检查回收治疗液，开门后等待复制人搬到安全地面，再恢复原病人。搬运中断或读档回仓重新派发，不丢身份；出仓已批准时等待搬运不倒退康复。正常出仓休养 3 周期，运动/力量 -3。紧急出仓运动/力量 -8，携带剩余康复时间/营养，等待或拆仓不会解除；选择该病人重新入仓只补剩余康复，不重复收费重建。",
            "暂停、报警与拆除","短暂断电或自动化红信号暂停、不倒退。康复水/氧耗尽时有效康复时间以真实时间 0.1 倍倒退；已付营养不撤销。缺食品只暂停。请求输出绿信号表示需要医生或补给；空仓为红。空闲/等待医生不收自动治疗电费。取消重建返还遗体，已用材料不退款；病人入仓后禁止普通拆除，先取消/紧急出仓。建筑被强制销毁时尽力释放病人并保留欠额。出仓需要附近同世界可站立且有头部空间的地格；阻塞时等待。"
        }:new[]{
            "Construction and connections","4x4 floor building, Medical menu, MedicineIV. Construction: 800 kg refined metal and 200 kg plastic. Bottom: water (-1,0), oxygen (1,0), power (0,0). Automation input (-1,1), medical-request output (1,1). Hospital recommended. Leave standing cells and two-cell headroom beside the chamber.",
            "Patients and identity","Cycle a standard corpse in this world, then request admission. One patient per chamber, native fetch chore and building priority. Pre-installation corpses are supported if still present. Graves destroy bodies; buried or destroyed duplicants cannot be recovered. Bionics unsupported. Native transfer retains original identity, appearance, traits, aptitude, resume, learned attributes, priorities, diet and schedule. Reassign beds and equipment.",
            "Costs","First reconstruction: 1800 s, 100 kg refined metal, 50 kg plastic, 200 kg water, 40 kg oxygen, 2400 W. Recovery: at least 3000 s, 300 kg water, 60 kg oxygen, 60000 kcal quality +2 food, 1200 W. Stored food still spoils and rotten food does not count. Repeat metal/plastic/nutrition multiplier: 1 + 0.5 times prior successful revivals. Water, oxygen and duration fixed. Startup needs all repair materials plus 60 kg water, 12 kg oxygen and 12000 times multiplier kcal.",
            "Doctors and schedules","Three examinations: startup, recovery admission and discharge, each 60 base seconds. Native CanDoctor perk and medical speed apply. Doctors obey work, meals and sleep. Interruptions, replacements and save/load retain examination and fluid progress. Arrival closes the door. The last 20 startup work seconds fill treatment fluid; the last 20 discharge work seconds drain it. No liquid output. Automatic treatment needs no attending doctor, including during sleep. Stage boundaries await a doctor without advancing treatment or charging resources.",
            "Recovery and discharge","Reconstruction enters native medical stasis, preserving the patient without colony work. After time and nutrition are settled, the doctor drains fluid and opens the door. A duplicant carries the patient to safe floor before the original patient is restored. Interrupted carry or save/load re-offers transport with identity still protected in the chamber. Approved discharge no longer regresses while waiting. Normal discharge: 3 cycles at Athletics/Strength -3. Emergency discharge: -8 and persistent remaining recovery time/nutrition. Waiting or demolition cannot clear this debt. Select the patient to return and complete remaining recovery without new reconstruction fees.",
            "Interruptions and alarms","Power loss or red automation pauses without regression. Empty recovery water/oxygen rolls effective time back at 0.1 times real time; paid nutrition remains paid. Food shortage pauses. Green request output means a doctor or supplies are needed. No treatment charge while idle or awaiting examination. Cancel returns a corpse; used supplies are not refunded. Discharge before normal demolition. Forced destruction attempts to release the patient with debt. Exit requires a same-world standing cell with headroom."
        };
    }
}
