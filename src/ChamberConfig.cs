using System.Collections.Generic;
using TUNING;
using UnityEngine;
namespace Baiye.Resurrection
{
    public sealed class ChamberConfig : IBuildingConfig
    {
        public static readonly float[] StorageLimits={200,1000,200,100000,50000,200};
        public override BuildingDef CreateBuildingDef()
        {
            var d=BuildingTemplates.CreateBuildingDef(ChamberStrings.ID,4,4,"baiye_reconstruction_kanim",100,60,new[]{800f,200f},new[]{"RefinedMetal","Plastic"},373.15f,BuildLocationRule.OnFloor,BUILDINGS.DECOR.PENALTY.TIER1,NOISE_POLLUTION.NOISY.TIER1);
            d.RequiresPowerInput=true; d.EnergyConsumptionWhenActive=2400;
            d.PowerInputOffset=new CellOffset(0,0); d.Overheatable=false;
            d.InputConduitType=ConduitType.Liquid;d.UtilityInputOffset=new CellOffset(-1,0);
            d.LogicInputPorts=LogicOperationalController.CreateSingleInputPortList(new CellOffset(-1,1));
            d.LogicOutputPorts=new List<LogicPorts.Port>{LogicPorts.Port.OutputPort(ChamberStrings.AttentionPort,new CellOffset(1,1),ChamberStrings.T("医疗请求","Medical request"),ChamberStrings.T("需要医生或补给","Doctor or supplies required"),ChamberStrings.T("无医疗请求","No request"))};
            d.AudioCategory="HollowMetal";ChamberCodex.Attach(d);return d;
        }
        public override void ConfigureBuildingTemplate(GameObject go,Tag prefabTag)
        {
            go.AddOrGet<Operational>();
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.Clinic);
            for(int i=0;i<StorageLimits.Length;i++)
            {
                var s=go.AddComponent<Storage>();s.capacityKg=StorageLimits[i];s.showInUI=true;
                s.allowItemRemoval=i!=0;s.allowUIItemRemoval=i!=0;s.fetchCategory=Storage.FetchCategory.Building;
                s.storageFilters=new List<Tag>();s.SetDefaultStoredItemModifiers(i==5?new List<Storage.StoredItemModifier>{Storage.StoredItemModifier.Hide}:Storage.StandardSealedStorage);
            }
            var stores=go.GetComponents<Storage>();
            Consumer(go,stores[1],ConduitType.Liquid,SimHashes.Water.CreateTag(),1000,10,false);
            Consumer(go,stores[2],ConduitType.Gas,SimHashes.Oxygen.CreateTag(),200,1,true);
            Deliver(go,stores[3],new Tag("RefinedMetal"),100);
            Deliver(go,stores[4],SimHashes.Polypropylene.CreateTag(),50);
            Deliver(go,stores[5],new Tag(ChamberStrings.FoodTag),200);
            go.AddOrGet<ChamberGasInput>();go.AddOrGet<MinionStorage>();
            go.AddOrGet<ChamberProcess>();go.AddOrGet<ChamberDoctor>();go.AddOrGet<ChamberPatientReturn>();go.AddOrGet<ChamberCarry>();go.AddOrGet<ChamberPatientVisual>();go.AddOrGet<ChamberVisuals>();
            for(int i=0;i<ChamberControl.Count;i++)go.AddComponent<ChamberControl>().Choice=i;
            Prioritizable.AddRef(go);
            var room=go.AddOrGet<RoomTracker>();room.requiredRoomType=Db.Get().RoomTypes.Hospital.Id;
            room.requirement=RoomTracker.Requirement.CustomRecommended;room.customStatusItemID=Db.Get().BuildingStatusItems.ClinicOutsideHospital.Id;
        }
        static void Consumer(GameObject go,Storage store,ConduitType type,Tag tag,float cap,float rate,bool secondary)
        {
            var c=go.AddComponent<ConduitConsumer>();c.storage=store;c.conduitType=type;c.capacityTag=tag;c.capacityKG=cap;c.consumptionRate=rate;c.useSecondaryInput=secondary;
            c.alwaysConsume=true;c.forceAlwaysSatisfied=true;c.wrongElementResult=ConduitConsumer.WrongElementResult.Store;
        }
        static void Deliver(GameObject go,Storage store,Tag tag,float cap)
        {
            var d=go.AddComponent<ManualDeliveryKG>();d.SetStorage(store);d.RequestedItemTag=tag;d.capacity=cap;
            d.refillMass=0;d.MinimumMass=.001f;d.choreTypeIDHash=Db.Get().ChoreTypes.DoctorFetch.IdHash;d.operationalRequirement=Operational.State.Functional;
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LogicOperationalController>();go.AddOrGet<KBatchedAnimHeatPostProcessingEffect>();
        }
    }
    public sealed class ChamberGasInput : KMonoBehaviour,ISecondaryInput
    {
        FlowUtilityNetwork.NetworkItem endpoint;
        public bool HasSecondaryConduitType(ConduitType type)=>type==ConduitType.Gas;
        public CellOffset GetSecondaryConduitOffset(ConduitType type)=>new CellOffset(1,0);
        protected override void OnSpawn()
        {
            base.OnSpawn();int cell=Grid.OffsetCell(GetComponent<Building>().NaturalBuildingCell(),new CellOffset(1,0));
            endpoint=new FlowUtilityNetwork.NetworkItem(ConduitType.Gas,Endpoint.Sink,cell,gameObject);
            Conduit.GetNetworkManager(ConduitType.Gas).AddToNetworks(cell,endpoint,is_endpoint:true);
        }
        protected override void OnCleanUp()
        {
            if(endpoint!=null)Conduit.GetNetworkManager(ConduitType.Gas).RemoveFromNetworks(endpoint.Cell,endpoint,is_endpoint:true);
            base.OnCleanUp();
        }
    }
}
