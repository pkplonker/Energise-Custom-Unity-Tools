using System;
using UnityEngine;

namespace CustomTick
{
	/// <summary>
	/// Fluent builder for registering tick functions with the TickManager.
	/// </summary>
	/// <remarks>
	/// Supports both parameterless <see cref="Action"/> delegates and methods on MonoBehaviours with optional parameters.
	/// Allows configuration of tick interval, delay, one-shot execution, and pause state.
	/// </remarks>
	public class TickBuilder
	{
		private Action action;
		private MonoBehaviour target;
		private string methodName;
		private object[] parameters;

		private float interval = 1f;
		private float delay;
		private bool oneShot;
		private bool paused;

		private bool isAction;
		private bool isMethodWithParams;

#if UNITY_EDITOR
		private string description;
#endif

		private TickBuilder() { }

		/// <summary>
		/// Creates a TickBuilder for an <see cref="Action"/> delegate.
		/// </summary>
		/// <param name="action">The method to invoke on each tick.</param>
		/// <returns>A new TickBuilder instance.</returns>
		public static TickBuilder Create(Action action) =>
			new()
			{
				action = action,
				isAction = true
			};

		/// <summary>
		/// Creates a TickBuilder for a method on a MonoBehaviour, with optional parameters.
		/// </summary>
		/// <param name="target">The MonoBehaviour containing the method.</param>
		/// <param name="methodName">The name of the method to call.</param>
		/// <param name="parameters">Optional parameters to pass to the method.</param>
		/// <returns>A new TickBuilder instance.</returns>
		public static TickBuilder Create(MonoBehaviour target, string methodName, object[] parameters = null) =>
			new()
			{
				target = target,
				methodName = methodName,
				parameters = parameters,
				isMethodWithParams = true
			};

		/// <summary>
		/// Sets the interval (in seconds) at which the tick should fire.
		/// </summary>
		/// <param name="seconds">The tick interval, must be greater than 0.</param>
		public TickBuilder SetInterval(float seconds)
		{
			interval = Mathf.Max(seconds, 0.001f);
			return this;
		}

		/// <summary>
		/// Sets an initial delay (in seconds) before the first tick executes.
		/// </summary>
		/// <param name="seconds">The delay before the first execution.</param>
		public TickBuilder SetDelay(float seconds)
		{
			delay = Mathf.Max(seconds, 0f);
			return this;
		}

		/// <summary>
		/// Specifies whether the tick should fire only once.
		/// </summary>
		/// <param name="value">True for one-shot, false for repeating (default: true).</param>
		public TickBuilder SetOneShot(bool value = true)
		{
			oneShot = value;
			return this;
		}

		/// <summary>
		/// Sets whether the tick should start in a paused state.
		/// </summary>
		/// <param name="value">True to start paused, false to start active (default: true).</param>
		public TickBuilder SetPaused(bool value = true)
		{
			paused = value;
			return this;
		}

		/// <summary>
		/// Finalizes the builder and registers the tick with the TickManager.
		/// </summary>
		/// <returns>A handle to the registered tick.</returns>
		public TickHandle Register()
		{
#if UNITY_EDITOR
			string finalDescription = description;
#endif

			if (isAction)
			{
#if UNITY_EDITOR
				if (string.IsNullOrEmpty(finalDescription) && action != null)
				{
					var method = action.Method;
					var targetName = method.DeclaringType?.Name ?? "Anon";
					finalDescription = $"{targetName}.{method.Name}";
				}
#endif
				return TickManager.Register(action, interval, delay, oneShot, paused
#if UNITY_EDITOR
					, finalDescription
#endif
				);
			}

			if (isMethodWithParams)
			{
#if UNITY_EDITOR
				if (string.IsNullOrEmpty(finalDescription) && target != null && !string.IsNullOrEmpty(methodName))
				{
					finalDescription = $"{target.GetType().Name}.{methodName}";
				}
#endif
				return TickManager.Register(target, methodName, parameters, interval, delay, oneShot, paused
#if UNITY_EDITOR
					, finalDescription
#endif
				);
			}

			Debug.LogWarning("TickBuilder: Invalid builder usage.");
			return default;
		}

		/// <summary>
		/// Sets an optional editor-only description for debugging and profiling purposes.
		/// </summary>
		/// <param name="desc">A label for the tick, shown in the editor.</param>
		public TickBuilder SetDescription(string desc)
		{
#if UNITY_EDITOR
			description = desc;
#endif
			return this;
		}
	}
}
