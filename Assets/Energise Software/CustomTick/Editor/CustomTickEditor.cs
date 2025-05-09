using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace CustomTick.Editor
{
	public class TickManagerWindow : EditorWindow
	{
		private Vector2 scroll;
		private string searchQuery = "";
		private bool showOnlyActive = false;
		private bool groupExpanded = true;
		private readonly Dictionary<float, bool> groupFoldouts = new();

		[MenuItem("Window/Energise Tools/Custom Tick")]
		public static void Open()
		{
			GetWindow<TickManagerWindow>("Tick Manager");
		}

		private void OnGUI()
		{
			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play Mode to view active ticks.", MessageType.Info);
				return;
			}

			DrawToolbar();

			scroll = EditorGUILayout.BeginScrollView(scroll);

			var groups = TickManager.EditorGetGroups();
			if (groups == null || groups.Count == 0)
			{
				EditorGUILayout.LabelField("No active ticks.");
				EditorGUILayout.EndScrollView();
				return;
			}

			foreach (var pair in groups.OrderBy(p => p.Key))
			{
				float interval = pair.Key;
				var group = pair.Value;
				var items = group.Items;

				if (!groupFoldouts.TryGetValue(interval, out var expanded))
					expanded = true;

				expanded = EditorGUILayout.Foldout(expanded,
					$"Interval Group: {interval:0.000}s | Items: {items.Count}", true);

				groupFoldouts[interval] = expanded;

				if (!expanded) continue;

				EditorGUI.indentLevel++;
				for (int i = 0; i < items.Count; i++)
				{
					var item = items[i];
					if (item == null) continue;
					if (FilterOut(item)) continue;

					DrawTickItem(item);
				}

				EditorGUI.indentLevel--;
				EditorGUILayout.Space(6);
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			searchQuery = GUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
			showOnlyActive = GUILayout.Toggle(showOnlyActive, "Active Only", EditorStyles.toolbarButton);

			EditorGUILayout.EndHorizontal();
		}

		private bool FilterOut(ITickItem item)
		{
			if (showOnlyActive && item.IsPaused())
				return true;

			if (string.IsNullOrEmpty(searchQuery))
				return false;

			string id = item.GetId().ToString();

			if (id.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
				return false;

#if UNITY_EDITOR
			if (TickManager.EditorTryGetType(item.GetId(), out var type))
			{
				if (type.ToString().IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
					return false;
			}
#endif
			return true;
		}

		private void DrawTickItem(ITickItem item)
		{
			string id = item.GetId().ToString();
			string status = item.IsValid() ? "Valid" : "Invalid";
			string paused = item.IsPaused() ? "Paused" : "Active";
			string once = item.IsOneShot() ? "OneShot" : "Loop";

			string type = TickManager.EditorTryGetType(item.GetId(), out var foundType)
				? foundType.ToString()
				: "Unknown";

			Color originalColor = GUI.color;

			if (!item.IsValid())
				GUI.color = Color.red;
			else if (item.IsPaused())
				GUI.color = Color.yellow;
			else if (item.IsOneShot())
				GUI.color = Color.cyan;

			EditorGUILayout.BeginHorizontal();
			string description = TickManager.EditorTryGetDescription(item.GetId(), out var desc) ? desc : "(Unnamed)";
			GUILayout.Label($"[ID:{id}] [{type}] [{paused}] [{once}] [{status}] {description}",
				GUILayout.ExpandWidth(true));

			if (GUILayout.Button(item.IsPaused() ? "Resume" : "Pause", GUILayout.Width(60)))
			{
				if (item.IsPaused())
				{
					TickManager.Resume(new TickHandle {Id = item.GetId()});
				}
				else
				{
					TickManager.Pause(new TickHandle {Id = item.GetId()});
				}
			}

			if (GUILayout.Button("Remove", GUILayout.Width(60)))
			{
				TickManager.Unregister(new TickHandle {Id = item.GetId()});
			}

			EditorGUILayout.EndHorizontal();

			GUI.color = originalColor;
		}

		private void OnDisable()
		{
			groupFoldouts.Clear();
		}
	}
}