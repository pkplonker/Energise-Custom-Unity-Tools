using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal class MemoryStats
	{
		internal Dictionary<ScriptableObject, long> memoryUsage = new();
		internal long totalMemoryAll = 0;
		internal long totalMemoryFiltered = 0;
	}
}