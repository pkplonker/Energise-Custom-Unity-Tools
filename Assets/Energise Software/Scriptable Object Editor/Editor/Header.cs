namespace ScriptableObjectEditor
{
	internal class Header
	{
		internal Header(string label, int width)
		{
			Label = label;
			Width = width;
		}

		internal string Label { get; set; }

		internal int Width { get; set; }
	}
}