namespace ScriptableObjectEditor
{
	internal class Header
	{
		internal Header(string label, float width)
		{
			Label = label;
			Width = width;
		}

		internal string Label { get; set; }

		internal float Width { get; set; }
	}
}