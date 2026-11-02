using System;
using System.Collections.Generic;
namespace Baiye.Resurrection.Rescue {
 public enum TargetKind { None, Living, Corpse }
 public enum CargoHomeDecision { Unload, Hold, DeliverCargo }
 public static class RescueRules {
  public static TargetKind Classify(bool standard,bool incapacitated,bool suffocating,bool dying,bool corpse,bool pendingDeath) {
   if (!standard) return TargetKind.None;
   if (corpse) return TargetKind.Corpse;
   return !dying && !pendingDeath && (incapacitated || suffocating) ? TargetKind.Living : TargetKind.None;
  }
  public static TargetKind ClassifyEscrow(bool verified,bool standard,bool dying,bool corpse,bool pendingDeath) => !verified || !standard ? TargetKind.None : corpse ? TargetKind.Corpse : dying || pendingDeath ? TargetKind.None : TargetKind.Living;
  public static CargoHomeDecision DecideCargoAtHome(bool cargoCorpse,bool corpseSlotFull,bool deliveryAttemptAllowed) => !cargoCorpse || !corpseSlotFull ? CargoHomeDecision.Unload : deliveryAttemptAllowed ? CargoHomeDecision.DeliverCargo : CargoHomeDecision.Hold;
  public static bool ReturnEmptyAfterCargoDelivery(bool completed) => completed;
  public static bool CanClaim(bool storage,bool chamber,bool rescue) => !storage && !chamber && !rescue;
  public static bool CanAccept(TargetKind kind,bool livingFull,bool corpseFull) => kind==TargetKind.Living ? !livingFull : kind==TargetKind.Corpse && !corpseFull;
  public static float HealedHealth(float current,float maximum) => Math.Max(current,maximum*.3f);
  public static bool FlightClearance(int cell,int width,int height,Func<int,bool> passable) =>
   width>0 && height>0 && cell>=width && (long)cell<(long)width*height && passable!=null && passable(cell) && passable(cell-width);
  public static int[] Path(int width,int height,int start,int goal,Func<int,bool> passable,int budget) {
   if(width<=0 || height<=0 || (long)width*height>int.MaxValue || start<0 || goal<0 || start>=width*height || goal>=width*height || budget<=0 || passable==null || !passable(start) || !passable(goal)) return null;
   var queue=new Queue<int>(); var previous=new Dictionary<int,int>(); queue.Enqueue(start); previous.Add(start,-1);
   while(queue.Count>0 && budget-->0) {
    int cell=queue.Dequeue();
    if(cell==goal) { var result=new List<int>(); for(int p=cell;p!=-1;p=previous[p]) result.Add(p); result.Reverse(); return result.ToArray(); }
    int x=cell%width, y=cell/width;
    if(x>0) Add(cell-1,cell,passable,previous,queue);
    if(x+1<width) Add(cell+1,cell,passable,previous,queue);
    if(y>0) Add(cell-width,cell,passable,previous,queue);
    if(y+1<height) Add(cell+width,cell,passable,previous,queue);
   }
   return null;
  }
  static void Add(int next,int from,Func<int,bool> passable,Dictionary<int,int> previous,Queue<int> queue) { if(!previous.ContainsKey(next) && passable(next)) { previous.Add(next,from); queue.Enqueue(next); } }
 }
 // The cargo remains aboard if it dies and the separate corpse slot is occupied.
 public sealed class RescueCapacity {
  public TargetKind Cargo { get; private set; }
  public bool LivingOccupied { get; private set; }
  public bool CorpseOccupied { get; private set; }
  public bool Load(TargetKind kind) { if(Cargo!=TargetKind.None || !RescueRules.CanAccept(kind,LivingOccupied,CorpseOccupied))return false; Cargo=kind; return true; }
  public void CargoDied() { if(Cargo==TargetKind.Living) Cargo=TargetKind.Corpse; }
  public bool Unload() { if(!RescueRules.CanAccept(Cargo,LivingOccupied,CorpseOccupied))return false; if(Cargo==TargetKind.Living)LivingOccupied=true; else CorpseOccupied=true; Cargo=TargetKind.None; return true; }
  public void Release(TargetKind kind) { if(kind==TargetKind.Living) LivingOccupied=false; else if(kind==TargetKind.Corpse) CorpseOccupied=false; }
 }
 public sealed class RescueClaims {
  readonly Dictionary<int,int> claims=new Dictionary<int,int>();
  public bool TryClaim(int target,int owner) { if(claims.TryGetValue(target,out int existing)) return existing==owner; claims.Add(target,owner); return true; }
  public void Release(int target,int owner) { if(claims.TryGetValue(target,out int existing) && existing==owner)claims.Remove(target); }
 }
}
