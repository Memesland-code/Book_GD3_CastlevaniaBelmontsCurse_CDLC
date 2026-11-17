#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof (PlayerStateSelectorAttribute))]
public class PlayerStateSelectorDrawer : PropertyDrawer
{
	private static string[] stateTypeNames;

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (stateTypeNames == null)
		{
			stateTypeNames = TypeCache.GetTypesDerivedFrom<PlayerState>()
				.Where(t => !t.IsAbstract)
				.Select(t => t.Name)
				.OrderBy(n => n, StringComparer.Ordinal).ToArray();
		}

		int currentIndex = Array.IndexOf(stateTypeNames, property.stringValue);
		if (currentIndex < 0) currentIndex = 0;
		
		int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, stateTypeNames);
		if (stateTypeNames.Length > 0)
		{
			property.stringValue = stateTypeNames[selectedIndex];
		}
	}
}
#endif