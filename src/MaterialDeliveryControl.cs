namespace Baiye.Resurrection;

public static class MaterialDeliveryControl
{
	public static void Apply(IMaterialDeliveryPort port, MaterialSupplyPlan plan, bool functional)
	{
		if (plan.CapacityKg - (double)port.Capacity > 0.001 && port.HasTask)
		{
			port.AbortDelivery("Medical material target changed");
		}
		port.Capacity = (float)plan.CapacityKg;
		port.RefillMass = 0f;
		port.Pause(!plan.CanRequest);
		if (!plan.CanRequest && port.HasTask)
		{
			port.AbortDelivery("Medical material reserve satisfied or storage full");
		}
		port.UpdateDeliveryState();
		if (plan.CanRequest && functional && !port.HasTask)
		{
			port.RequestDelivery();
		}
	}
}
