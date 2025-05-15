using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectEditor
{
	internal static class SOEIO
	{
		/// <summary>
		/// Generates a non-colliding asset path by appending or incrementing a numeric suffix.
		/// </summary>
		/// <param name="originalPath">Original asset path including filename and extension.</param>
		/// <param name="uniquePath">Outputs a new unique path if successful.</param>
		/// <returns>True if a unique path was found; false otherwise.</returns>
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

		/// <summary>
		/// Creates a duplicate of the given ScriptableObject at a unique path next to the original.
		/// </summary>
		/// <param name="add">The ScriptableObject to clone and save.</param>
		private static void AddAsset(ScriptableObject add)
		{
			var assetPath = AssetDatabase.GetAssetPath(add);
			if (TryGetUniqueAssetPath(assetPath, out var newAssetPath))
			{
				var newObj = Object.Instantiate(add);
				AssetDatabase.CreateAsset(newObj, newAssetPath);
				AssetDatabase.SaveAssets();
			}
		}

		/// <summary>
		/// Deletes the specified ScriptableObject asset from the project.
		/// </summary>
		/// <param name="scriptableObject">The ScriptableObject asset to remove.</param>
		private static void RemoveAsset(ScriptableObject scriptableObject)
		{
			var assetPath = AssetDatabase.GetAssetPath(scriptableObject);
			AssetDatabase.DeleteAsset(assetPath);
			AssetDatabase.SaveAssets();
		}

		/// <summary>
		/// Deletes a list of ScriptableObject assets from the project.
		/// </summary>
		/// <param name="toRemove">List of ScriptableObjects to delete.</param>
		internal static void RemoveAssets(List<ScriptableObject> toRemove)
		{
			foreach (var obj in toRemove)
			{
				RemoveAsset(obj);
			}
		}

		/// <summary>
		/// Duplicates and saves a list of ScriptableObject assets, each to a unique path.
		/// </summary>
		/// <param name="toAdd">List of ScriptableObjects to clone and save.</param>
		internal static void AddAssets(List<ScriptableObject> toAdd)
		{
			foreach (var add in toAdd)
			{
				AddAsset(add);
			}
		}
	}
}