using UnityEngine;
using System;

namespace CustomTick
{
	/// <summary>
	/// Marks a method to be automatically registered with the TickManager and invoked at a fixed interval.
	/// </summary>
	/// <remarks>
	/// Use this attribute on parameterless methods inside MonoBehaviours to have them tick automatically during playmode.
	/// Ticks are grouped by interval and run centrally with zero hot-path GC allocations.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class TickAttribute : Attribute
	{
		/// <summary>
		/// The interval (in seconds) between tick executions.
		/// Must be greater than 0.
		/// </summary>
		public float Interval { get; private set; }

		/// <summary>
		/// Optional delay (in seconds) before the first tick executes.
		/// </summary>
		public float Delay { get; private set; }

		/// <summary>
		/// Creates a new TickAttribute with a fixed interval and optional delay.
		/// </summary>
		/// <param name="interval">How often the method should be called, in seconds.</param>
		/// <param name="delay">Optional delay before the first execution, in seconds.</param>
		public TickAttribute(float interval, float delay = 0f)
		{
			Interval = Mathf.Max(interval, 0f);
			Delay = Mathf.Max(delay, 0f);
		}
	}
}