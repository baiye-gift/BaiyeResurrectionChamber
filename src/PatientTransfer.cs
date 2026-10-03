using System;
using Klei.AI;
using UnityEngine;
namespace Baiye.Resurrection
{
    public static class PatientTransfer
    {
        public static bool Eligible(MinionIdentity id)
        {
            if(id==null||id.model!=MinionConfig.MODEL||id.gameObject.HasTag(GameTags.Stored))return false;
            return id.gameObject.HasTag(GameTags.Corpse)||id.GetComponent<PatientRecord>()?.NeedsRecovery==true;
        }
        public static void EnterStasis(MinionStorage storage,GameObject patient,TreatmentModel model,bool newRevival)
        {
            if(storage.GetStoredMinionInfo().Count!=0)throw new InvalidOperationException("Patient already in stasis");
            var record=patient.GetComponent<PatientRecord>();
            if(record==null)throw new InvalidOperationException("Missing patient save contract");
            record.Recovery=model.Debt();
            storage.SerializeMinion(patient);
            var info=storage.GetStoredMinionInfo();
            if(info.Count!=1||info[0].serializedMinion.Get()==null)throw new InvalidOperationException("Native stasis did not retain patient");
            var stored=info[0].serializedMinion.Get().GetComponent<PatientRecord>();
            if(newRevival)stored.SuccessfulRevivals=model.PriorRevivals+1;
            stored.Recovery=model.Debt();
        }
        public static GameObject Release(MinionStorage storage,TreatmentModel model,Vector3 position,bool emergency)
        {
            var list=storage.GetStoredMinionInfo();if(list.Count!=1)return null;
            var saved=list[0].serializedMinion.Get();if(saved==null)return null;
            saved.GetComponent<PatientRecord>().Recovery=emergency?model.Debt():null;
            // This native route restores identity, resume, learned attribute levels,
            // appearance, priorities, food restrictions, schedule and original ID.
            var patient=storage.DeserializeMinion(list[0].id,position);
            if(patient==null)return null;
            var effects=patient.GetComponent<Effects>();
            foreach(string id in new[]{"LightWounds","ModerateWounds","SevereWounds",ChamberStrings.WeakEffect,ChamberStrings.SevereEffect})effects.Remove(id);
            effects.Add(emergency?ChamberStrings.SevereEffect:ChamberStrings.WeakEffect,should_save:true);
            var hp=patient.GetComponent<Health>();hp.hitPoints=hp.maxHitPoints;hp.OnHealthChanged(0);
            foreach(var amount in new[]{Db.Get().Amounts.Calories,Db.Get().Amounts.Breath,Db.Get().Amounts.Stamina})
            {
                var value=amount.Lookup(patient);if(value!=null)value.SetValue(value.GetMax());
            }
            var bladder=Db.Get().Amounts.Bladder.Lookup(patient);if(bladder!=null)bladder.SetValue(bladder.GetMin());
            patient.GetComponent<Navigator>().SetCurrentNavType(NavType.Floor);
            patient.GetComponent<KSelectable>().SetStatusItem(Db.Get().StatusItemCategories.Main,null);
            Debug.Log("[BaiyeResurrectionChamber] Released original patient "+patient.GetProperName()+" id="+patient.GetComponent<KPrefabID>().InstanceID+" emergency="+emergency);
            return patient;
        }
        public static int ExitCell(GameObject chamber)
        {
            int origin=Grid.PosToCell(chamber),world=chamber.GetMyWorldId();
            // Prefer adjacent doors, then nearby floor. Check headroom and world.
            foreach(int x in new[]{-2,3,-3,4,-4,5,0,1,-1,2})
            {
                int cell=Grid.OffsetCell(origin,new CellOffset(x,0));
                if(IsSafeCell(cell,world))return cell;
            }
            return Grid.InvalidCell;
        }
        public static bool IsSafeCell(int cell,int world)
        {
            if(!Grid.IsValidCell(cell))return false;
            int head=Grid.CellAbove(cell),floor=Grid.CellBelow(cell);
            return Grid.IsValidCell(head)&&Grid.IsValidCell(floor)&&Grid.WorldIdx[cell]==world&&!Grid.Solid[cell]&&!Grid.Solid[head]&&Grid.Solid[floor];
        }
    }
}
