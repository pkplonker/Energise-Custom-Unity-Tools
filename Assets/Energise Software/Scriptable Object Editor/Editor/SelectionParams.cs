namespace ScriptableObjectEditor
{
	internal class SelectionParams
	{
		internal string instanceSearchString = string.Empty;
		internal bool includeDerivedTypes = true;
		internal string assetsFolderPath = "Assets";
		internal string typeSearchString = string.Empty;
		internal int selectedTypeIndex;
		internal int selectedAssemblyIndex;
	}
}