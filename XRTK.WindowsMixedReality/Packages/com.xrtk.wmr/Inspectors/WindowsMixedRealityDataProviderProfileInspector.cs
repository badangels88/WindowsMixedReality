// Copyright (c) XRTK. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using UnityEngine;
using XRTK.Inspectors.Profiles;
using XRTK.Inspectors.Utilities;
using XRTK.WindowsMixedReality.Profiles;

namespace XRTK.WindowsMixedReality.Inspectors
{
    [CustomEditor(typeof(WindowsMixedRealityControllerDataProviderProfile))]
    public class WindowsMixedRealityDataProviderProfileInspector : BaseMixedRealityProfileInspector
    {
        private SerializedProperty gestureRecognitionStartBehaviour;
        private SerializedProperty manipulationGestureSettings;
        private SerializedProperty useRailsNavigation;
        private SerializedProperty railsNavigationGestureSettings;

        protected override void OnEnable()
        {
            base.OnEnable();

            gestureRecognitionStartBehaviour = serializedObject.FindProperty(nameof(gestureRecognitionStartBehaviour));
            manipulationGestureSettings = serializedObject.FindProperty(nameof(manipulationGestureSettings));
            useRailsNavigation = serializedObject.FindProperty(nameof(useRailsNavigation));
            railsNavigationGestureSettings = serializedObject.FindProperty(nameof(railsNavigationGestureSettings));
        }

        public override void OnInspectorGUI()
        {
            MixedRealityInspectorUtility.RenderMixedRealityToolkitLogo();

            if (thisProfile.ParentProfile != null &&
                GUILayout.Button("Back to Controller Data Providers"))
            {
                Selection.activeObject = thisProfile.ParentProfile;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Windows Mixed Reality Controller Data Provider Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This profile aids in configuring additional platform settings for the registered controller data provider. This can be anything from additional gestures or platform specific settings.", MessageType.Info);

            thisProfile.CheckProfileLock();

            serializedObject.Update();

            EditorGUILayout.BeginVertical("Label");
            EditorGUILayout.Space();

            if (MixedRealityInspectorUtility.CheckProfilePlatform(Definitions.Utilities.SupportedPlatforms.WindowsUniversal | Definitions.Utilities.SupportedPlatforms.Editor))
            {
                EditorGUILayout.LabelField("Windows Gesture Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(gestureRecognitionStartBehaviour);
                EditorGUILayout.PropertyField(manipulationGestureSettings);
                EditorGUILayout.PropertyField(useRailsNavigation);
                EditorGUILayout.PropertyField(railsNavigationGestureSettings);
            }

            EditorGUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
        }
    }
}