using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectEditor
{
	/// <summary>
	/// Factory for built-in columns (Copy, Delete, Instance Name).
	/// </summary>
	internal static class BuiltInColumnFactory
	{
		/// <summary>
		/// Returns the default set of built-in columns: Copy, Delete, and Instance Name.
		/// </summary>
		public static IEnumerable<Column> CreateDefaults()
		{
			yield return CreateCopy();
			yield return CreateDelete();
			yield return CreateInstanceName();
		}

		/// <summary>
		/// Creates the “Copy” built-in column which duplicates a ScriptableObject when clicked.
		/// </summary>
		private static Column CreateCopy() => new(
			Column.ColumnType.BuiltIn, "Copy", 65f, null,
			(obj, toAdd, toRemove, opts) =>
			{
				if (GUILayout.Button(
					    EditorGUIUtility.IconContent("d_Toolbar Plus", "|Copy this scriptable object"), opts))
					toAdd.Add(obj);
			});

		/// <summary>
		/// Creates the “Delete” built-in column which removes a ScriptableObject when clicked.
		/// </summary>
		private static Column CreateDelete() => new(
			Column.ColumnType.BuiltIn, "Delete", 80f, null,
			(obj, toAdd, toRemove, opts) =>
			{
				if (GUILayout.Button(
					    EditorGUIUtility.IconContent("d_TreeEditor.Trash", "|Delete this scriptable object"), opts))
					toRemove.Add(obj);
			});

		/// <summary>
		/// Creates the “Instance Name” built-in column which displays and allows renaming of the asset.
		/// </summary>
		private static Column CreateInstanceName() => new(
			Column.ColumnType.BuiltIn, "Instance Name", 150f, null,
			(obj, toAdd, toRemove, opts) =>
			{
				var path = AssetDatabase.GetAssetPath(obj);
				var oldName = obj.name;
				var newName = EditorGUILayout.TextField(oldName, opts);
				if (newName != oldName)
				{
					AssetDatabase.RenameAsset(path, newName);
					AssetDatabase.SaveAssets();
					obj.name = newName;
				}
			});
	}
}