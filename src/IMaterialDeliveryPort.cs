namespace Baiye.Resurrection;

public interface IMaterialDeliveryPort
{
	float Capacity { get; set; }

	float RefillMass { get; set; }

	bool HasTask { get; }

	void Pause(bool paused);

	void AbortDelivery(string reason);

	void UpdateDeliveryState();

	void RequestDelivery();
}
