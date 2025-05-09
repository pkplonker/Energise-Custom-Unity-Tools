// Put this script in an Editor folder anywhere under Assets/
using UnityEditor;
using UnityEngine;

static class SceneGameTabContextMenu
{
	// This adds “My Scene Command” into the context menu when you right‑click
	// the Scene tab (i.e. the SceneView window).
	[MenuItem("CONTEXT/SceneView/My Scene Command")]
	static void MySceneCommand(MenuCommand cmd)
	{
		Debug.Log("Scene tab command executed!");
	}

	// Optionally you can enable/disable it via a validate function:
	[MenuItem("CONTEXT/SceneView/My Scene Command", true)]
	static bool MySceneCommand_Validate(MenuCommand cmd)
	{
		// for example only enable when not in play‑mode
		return !EditorApplication.isPlaying;
	}

	// This adds “My Game Command” into the context menu when you right‑click
	// the Game tab (i.e. the GameView window).
	[MenuItem("CONTEXT/GameView/My Game Command")]
	static void MyGameCommand(MenuCommand cmd)
	{
		Debug.Log("Game tab command executed!");
	}
}