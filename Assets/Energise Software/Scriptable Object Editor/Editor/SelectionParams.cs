namespace ScriptableObjectEditor
{
	public class SelectionParams
	{
		public string instanceSearchString = string.Empty;
		public bool includeDerivedTypes = true;
		public string assetsFolderPath = "Assets";
		public string typeSearchString;
		public int selectedTypeIndex;
		public int selectedAssemblyIndex;
	}
}