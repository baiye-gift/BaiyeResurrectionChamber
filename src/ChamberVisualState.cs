namespace Baiye.Resurrection
{
    // Rendering follows the real occupant and paid treatment, not a reservation.
    public static class ChamberVisualState
    {
        public static string Select(TreatmentStage stage,bool patientPresent,bool operational,bool blocked,bool recentlyPaid,bool releasing)
        {
            if(releasing&&!patientPresent)return "release";
            if(!patientPresent)return "idle";
            if(!operational)return "paused";
            if(stage==TreatmentStage.Rebuilding)return !blocked&&recentlyPaid?"reconstruct":"paused";
            if(stage==TreatmentStage.Recovering)return !blocked&&recentlyPaid?"recover":"alarm";
            return "loaded";
        }
    }
}
