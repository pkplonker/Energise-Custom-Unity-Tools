using System.Reflection;
using UnityEngine;

namespace CustomTick
{
	internal class TickMethodWithParams : TickItemBase
	{
		private MonoBehaviour Target;
		private MethodInfo Method;
		private object[] Parameters;
		private bool isValid;

		public TickMethodWithParams(int id, MonoBehaviour target, MethodInfo method, float interval,
			object[] parameters, float delay, bool oneShot, bool paused)
			: base(id, interval, delay, oneShot, paused)
		{
			Target = target;
			Method = method;
			Parameters = parameters;
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
				Method?.Invoke(Target, Parameters);
			}
		}
	}
}