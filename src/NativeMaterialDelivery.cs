namespace Baiye.Resurrection;

internal sealed class NativeMaterialDelivery : IMaterialDeliveryPort
{
	private readonly ManualDeliveryKG delivery;

	public float Capacity
	{
		get
		{
			return delivery.capacity;
		}
		set
		{
			delivery.capacity = value;
		}
	}

	public float RefillMass
	{
		get
		{
			return delivery.refillMass;
		}
		set
		{
			delivery.refillMass = value;
		}
	}

	public bool HasTask
	{
		get
		{
			if (delivery.DebugFetchList != null)
			{
				return !delivery.DebugFetchList.IsComplete;
			}
			return false;
		}
	}

	public NativeMaterialDelivery(ManualDeliveryKG delivery)
	{
		this.delivery = delivery;
	}

	public void Pause(bool paused)
	{
		delivery.Pause(paused, "Medical reconstruction reserve");
	}

	public void AbortDelivery(string reason)
	{
		delivery.AbortDelivery(reason);
	}

	public void UpdateDeliveryState()
	{
		delivery.UpdateDeliveryState();
	}

	public void RequestDelivery()
	{
		delivery.RequestDelivery();
	}
}
