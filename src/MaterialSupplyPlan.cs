using System;

namespace Baiye.Resurrection;

public struct MaterialSupplyPlan
{
	public const double RoundingToleranceKg = 0.01;

	public const double MinimumBatchKg = 0.01;

	public double TargetKg;

	public double MissingKg;

	public double CapacityKg;

	public bool NeedsMaterial;

	public bool CanRequest;

	public static double ReserveTarget(TreatmentModel model, bool occupied, bool plastic)
	{
		if (!occupied || (model.Stage != TreatmentStage.Empty && model.Stage != TreatmentStage.StartCheck && model.Stage != TreatmentStage.Rebuilding))
		{
			return 0.0;
		}
		if (model.Stage == TreatmentStage.Rebuilding)
		{
			Bill bill = model.Preview(1800.0);
			if (!plastic)
			{
				return bill.Metal;
			}
			return bill.Plastic;
		}
		return (double)(plastic ? 50 : 100) * model.Multiplier;
	}

	public static MaterialSupplyPlan Create(double target, double mass, double totalMass, double storageCapacity, double requiredNow = 0.0)
	{
		MaterialSupplyPlan result = new MaterialSupplyPlan
		{
			TargetKg = Nonnegative(target)
		};
		mass = Nonnegative(mass);
		result.MissingKg = Math.Max(0.0, result.TargetKg - mass);
		result.NeedsMaterial = result.MissingKg > 0.01 || mass + 1E-07 < Nonnegative(requiredNow);
		double num = Math.Max(0.0, Nonnegative(storageCapacity) - Nonnegative(totalMass));
		result.CanRequest = result.NeedsMaterial && num > 0.001;
		result.CapacityKg = mass + (result.CanRequest ? Math.Min(num, Math.Max(0.01, result.MissingKg)) : 0.0);
		return result;
	}

	private static double Nonnegative(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value) || !(value > 0.0))
		{
			return 0.0;
		}
		return value;
	}
}
