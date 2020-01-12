using UnityEngine;
using XRTK.Definitions.Utilities;
using XRTK.Definitions.Controllers;

#if WINDOWS_UWP
using XRTK.Attributes;
using Windows.UI.Input.Spatial;
#endif // WINDOWS_UWP

namespace XRTK.WindowsMixedReality.Profiles
{
    [CreateAssetMenu(menuName = "Mixed Reality Toolkit/Input System/Controller Data Providers/Windows Mixed Reality", fileName = "WindowsMixedRealityControllerDataProviderProfile", order = (int)CreateProfileMenuItemIndices.Input)]
    public class WindowsMixedRealityControllerDataProviderProfile : BaseMixedRealityControllerDataProviderProfile
    {
#if WINDOWS_UWP

        [Header("Gesture Recognition Settings")]

        [SerializeField]
        [Tooltip("Should gesture recognition auto start?")]
        private AutoStartBehavior gestureRecognitionStartBehaviour = AutoStartBehavior.AutoStart;

        /// <summary>
        /// Gets the configured start behavioru for gesture recognition.
        /// </summary>
        public AutoStartBehavior GestureRecognitionStartBehaviour => gestureRecognitionStartBehaviour;

        [EnumFlags]
        [SerializeField]
        [Tooltip("Settings for manipulation gesture recognition.")]
        private SpatialGestureSettings manipulationGestureSettings = SpatialGestureSettings.Tap | SpatialGestureSettings.ManipulationTranslate
            | SpatialGestureSettings.NavigationX | SpatialGestureSettings.NavigationY | SpatialGestureSettings.NavigationZ;

        /// <summary>
        /// Gets active settings for manipulation gesture recognition.
        /// </summary>
        public SpatialGestureSettings ManipulationGestureSettings => manipulationGestureSettings;

        [SerializeField]
        [Tooltip("Should the Navigation use Rails on start?\nNote: This can be changed at runtime to switch between the two Navigation settings.")]
        private bool useRailsNavigation = false;

        /// <summary>
        /// Should the Navigation use Rails on start?\nNote: This can be changed at runtime to switch between the two Navigation settings.
        /// </summary>
        public bool UseRailsNavigation => useRailsNavigation;

        [EnumFlags]
        [SerializeField]
        [Tooltip("Settings for rails navigation gesture recognition.")]
        private SpatialGestureSettings railsNavigationGestureSettings = SpatialGestureSettings.NavigationRailsX | SpatialGestureSettings.NavigationRailsY | SpatialGestureSettings.NavigationRailsZ;

        /// <summary>
        /// Gets active settings for rails navigation gesture recognition.
        /// </summary>
        public SpatialGestureSettings RailsNavigationGestureSettings => railsNavigationGestureSettings;

#endif // WINDOWS_UWP
    }
}
