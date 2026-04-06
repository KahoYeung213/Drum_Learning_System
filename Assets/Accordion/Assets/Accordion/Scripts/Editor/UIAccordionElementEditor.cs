using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace UnityEditor.UI
{
	[CustomEditor(typeof(UIAccordionElement), true)]
	public class UIAccordionElementEditor : ToggleEditor {
	
		public override void OnInspectorGUI()
		{
			this.serializedObject.Update();

			EditorGUILayout.LabelField("Accordion Element", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_MinHeight"));
			EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_AutoMinHeightFromHeader"));
			EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_HeaderTransform"));
			EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_HeaderPadding"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Hover", EditorStyles.boldLabel);
			SerializedProperty enableHover = this.serializedObject.FindProperty("m_EnableHoverColor");
			EditorGUILayout.PropertyField(enableHover);
			if (enableHover != null && enableHover.boolValue)
			{
				EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_HoverColor"));
				EditorGUILayout.PropertyField(this.serializedObject.FindProperty("m_HoverTargetGraphic"));
			}

			this.serializedObject.ApplyModifiedProperties();

			base.serializedObject.Update();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Toggle", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(base.serializedObject.FindProperty("m_IsOn"));
			EditorGUILayout.PropertyField(base.serializedObject.FindProperty("m_Interactable"));
			base.serializedObject.ApplyModifiedProperties();
		}
	}
}