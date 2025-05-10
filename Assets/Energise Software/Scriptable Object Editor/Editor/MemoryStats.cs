using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjectEditor
{
	public class MemoryStats
	{
		public Dictionary<ScriptableObject, long> memoryUsage = new();
		public long totalMemoryAll = 0;
		public long totalMemoryFiltered = 0;
	}
}