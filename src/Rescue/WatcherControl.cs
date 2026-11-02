using UnityEngine;
namespace Baiye.Resurrection.Rescue {
 public sealed class WatcherControl : KMonoBehaviour,ISidescreenButtonControl {
  WatcherStation Station=>GetComponent<WatcherStation>();
  public string SidescreenTitle=>ChamberStrings.T("守望者救援","Watcher rescue");
  public string SidescreenButtonText=>(Station.Phase==WatcherPhase.Outbound||Station.Phase==WatcherPhase.OutboundFallback)?ChamberStrings.T("取消本次出动","Cancel dispatch"):ChamberStrings.T("安全释放站内对象","Release held patient or body safely");
  public string SidescreenButtonTooltip=>ChamberStrings.T("出动中取消后无人机返航；站内对象仅在找到安全地面时释放。","Cancel an outbound dispatch and return home; held objects are released only onto safe ground.");
  public bool SidescreenEnabled()=>Station!=null;
  public bool SidescreenButtonInteractable()=>Station!=null&&((Station.Phase==WatcherPhase.Outbound||Station.Phase==WatcherPhase.OutboundFallback)||Station.CanRelease);
  public void OnSidescreenButtonPressed(){if((Station.Phase==WatcherPhase.Outbound||Station.Phase==WatcherPhase.OutboundFallback))Station.CancelOutbound();else Station.ReleaseSafely();}
  public int HorizontalGroupID()=>-1; public int ButtonSideScreenSortOrder()=>90; public void SetButtonTextOverride(ButtonMenuTextOverride textOverride){}
 }
}
