using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectEditor
{
	public partial class ScriptableObjectEditorWindow
	{
		/// <summary>
		/// Handles updating of asset types when assets are loaded
		/// </summary>
		public class ScriptableObjectPostprocessor : AssetPostprocessor
		{
			/// <summary>
			/// Callback for when assets are updated
			/// </summary>
			/// <param name="importedAssets">The new assets</param>
			/// <param name="deletedAssets">The deleted assets</param>
			/// <param name="movedAssets">The moved assets</param>
			/// <param name="movedFromAssetPaths">The moved from assets path</param>
			static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
				string[] movedFromAssetPaths)
			{
				bool refreshNeeded = importedAssets
					.Concat(deletedAssets)
					.Concat(movedAssets)
					.Any(assetPath => assetPath.EndsWith(".asset"));
				if (refreshNeeded)
				{
					var windows = Resources.FindObjectsOfTypeAll<ScriptableObjectEditorWindow>();
					foreach (var window in windows)
					{
						window.LoadObjectsOfType(window.scriptableObjectTypes[window.selectedTypeIndex]);
					}
				}
			}
		}
	}
}