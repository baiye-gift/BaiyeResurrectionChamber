using UnityEngine;
namespace Baiye.Resurrection
{
    public sealed class ChamberControl : KMonoBehaviour,ISidescreenButtonControl
    {
        public const int Count=3;
        [SerializeField] public int Choice;
        ChamberProcess Process=>GetComponent<ChamberProcess>();
        public string SidescreenTitle=>ChamberStrings.T("生命重建与康复","Reconstruction and recovery");
        public string SidescreenButtonText=>Choice==0?ChamberStrings.T("选择病人 → ","Select patient → ")+(Process.Occupied?Process.PatientName:Process.TargetName):Choice==1?ChamberStrings.T("安排入仓","Request admission"):Process.HasStasis?ChamberStrings.T("紧急出仓（保留康复欠额）","Emergency discharge (debt retained)"):ChamberStrings.T("取消治疗 / 入仓","Cancel treatment / admission");
        public string SidescreenButtonTooltip=>Choice==0?ChamberStrings.T("循环选择本世界仍存在的普通复制人遗体，或紧急出仓后需要继续康复的人；已被其他仓领取者跳过。选择后点击安排入仓。","Cycle existing standard corpses or recovering patients in this world. Claimed patients are excluded. Then request admission."):Choice==1?ChamberStrings.T("遗体由复制人搬入，恢复期病人自行走回。重建启动需医生、全量金属/塑料及一周期康复补给。","A duplicant carries the selected corpse; a recovering patient walks back. Startup requires a doctor, all repair materials and one cycle of recovery stock."):ChamberStrings.T("重建前可退回遗体；重建后紧急释放携带严重虚弱，回仓完成剩余康复才能解除。已用材料不退款。请先出仓再拆除建筑。","Before reconstruction finishes, return the corpse. Afterwards emergency discharge retains severe weakness and recovery debt. Used materials are not refunded. Empty the chamber before demolition.");
        public bool SidescreenEnabled()=>Process!=null;
        public bool SidescreenButtonInteractable()=>Process!=null&&(Choice==0?!Process.Occupied:Choice==1?!Process.Occupied&&Process.Target!=null:Process.Occupied&&(!Process.HasStasis||PatientTransfer.ExitCell(gameObject)!=Grid.InvalidCell));
        public void OnSidescreenButtonPressed()
        {
            if(!SidescreenButtonInteractable())return;
            if(Choice==0)Process.CycleTarget();else if(Choice==1)Process.Request();else Process.Cancel();
        }
        public int HorizontalGroupID()=>-1;
        public int ButtonSideScreenSortOrder()=>90;
        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride){}
    }
}
