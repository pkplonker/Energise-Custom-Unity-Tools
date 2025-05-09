using System.Reflection;
using UnityEngine;

namespace CustomTick
{
	internal class TickMethod : TickItemBase
	{
		private MonoBehaviour Target;
		private MethodInfo Method;
		private bool isValid;

		public TickMethod(int id, MonoBehaviour target, MethodInfo method, float interval, float delay, bool oneShot,
			bool paused)
			: base(id, interval, delay, oneShot, paused)
		{
			Target = target;
			Method = method;
			isValid = (target != null);
		}

		protected override bool ValidateTarget()
		{
			if (!isValid)
				return false;

			if (Target == null)
			{
				isValid = false;
				return false;
			}

			return true;
		}

		public override void Execute()
		{
			if (isValid)
			{
				Method?.Invoke(Target, null);
			}
		}
	}
}