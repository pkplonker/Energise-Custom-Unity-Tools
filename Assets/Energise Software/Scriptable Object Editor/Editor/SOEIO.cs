using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal static class SOEIO
	{
		internal static bool TryGetUniqueAssetPath(string originalPath, out string uniquePath)
		{
			uniquePath = null;
			try
			{
				var directory = Path.GetDirectoryName(originalPath);
				var extension = Path.GetExtension(originalPath);
				var name = Path.GetFileNameWithoutExtension(originalPath);
				if (directory == null) return false;

				int pos = name.Length - 1;
				while (pos >= 0 && char.IsDigit(name[pos])) pos--;
				bool hasNumber = pos < name.Length - 1;
				string baseName = hasNumber ? name.Substring(0, pos + 1) : name;
				int index = 0;
				if (hasNumber && !int.TryParse(name.Substring(pos + 1), out index)) index = 0;

				do
				{
					index++;
					var candidate = baseName + (hasNumber ? index.ToString() : "_Copy" + index) + extension;
					uniquePath = Path.Combine(directory, candidate);
				} while (File.Exists(uniquePath));

				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void AddAsset(ScriptableObject add)
		{
			var assetPath = AssetDatabase.GetAssetPath(add);
			if (TryGetUniqueAssetPath(assetPath, out var newAssetPath))
			{
				var newObj =Object.Instantiate(add);
				AssetDatabase.CreateAsset(newObj, newAssetPath);
				AssetDatabase.SaveAssets();
			}
		}

		private static void RemoveAsset(ScriptableObject scriptableObject)
		{
			var assetPath = AssetDatabase.GetAssetPath(scriptableObject);
			AssetDatabase.DeleteAsset(assetPath);
			AssetDatabase.SaveAssets();
		}

		internal static void RemoveAssets(List<ScriptableObject> toRemove)
		{
			foreach (var obj in toRemove)
			{
				RemoveAsset(obj);
			}
		}

		internal static void AddAssets(List<ScriptableObject> toAdd)
		{
			foreach (var add in toAdd)
			{
				AddAsset(add);
			}
		}
	}
}