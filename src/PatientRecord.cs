using KSerialization;
using UnityEngine;
namespace Baiye.Resurrection
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class PatientRecord : KMonoBehaviour, ISaveLoadable, StoredMinionIdentity.IStoredMinionExtension
    {
        [Serialize] public int SuccessfulRevivals;
        [Serialize] public RecoveryDebt Recovery;
        public bool NeedsRecovery => Recovery!=null && Recovery.Active;
        public void PushTo(StoredMinionIdentity destination) => CopyData(this,destination.GetComponent<PatientRecord>());
        public void PullFrom(StoredMinionIdentity source) => CopyData(source.GetComponent<PatientRecord>(),this);
        public void AddStoredMinionGameObjectRequirements(GameObject storedMinionGameObject) => storedMinionGameObject.AddOrGet<PatientRecord>();
        public static void CopyData(PatientRecord from,PatientRecord to)
        {
            if(object.ReferenceEquals(from,null)||object.ReferenceEquals(to,null))return;
            to.SuccessfulRevivals=from.SuccessfulRevivals;
            to.Recovery=from.Recovery==null?null:new RecoveryDebt{Active=from.Recovery.Active,PriorRevivals=from.Recovery.PriorRevivals,RecoveryTime=from.Recovery.RecoveryTime,NutritionPaid=from.Recovery.NutritionPaid};
        }
    }
}
