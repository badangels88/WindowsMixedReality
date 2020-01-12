// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XRTK.Providers.Controllers;
using XRTK.WindowsMixedReality.Profiles;

#if WINDOWS_UWP
using System;
using Windows.ApplicationModel.Core;
using Windows.Perception;
using Windows.Storage.Streams;
using Windows.UI.Input.Spatial;
using XRTK.Utilities;
using XRTK.WindowsMixedReality.Extensions;
using XRTK.Interfaces.InputSystem;
using System.Collections.Generic;
using UnityEngine;
using XRTK.Definitions.Devices;
using XRTK.Definitions.InputSystem;
using XRTK.Definitions.Utilities;
using XRTK.Services;
using Windows.Perception.Spatial;
using XRTK.WindowsMixedReality.Interfaces.Providers.Controllers;
#endif // WINDOWS_UWP

namespace XRTK.WindowsMixedReality.Controllers
{
    /// <summary>
    /// The device manager for Windows Mixed Reality controllers.
    /// </summary>
    public class WindowsMixedRealityControllerDataProvider : BaseControllerDataProvider
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="priority"></param>
        /// <param name="profile"></param>
        public WindowsMixedRealityControllerDataProvider(string name, uint priority, WindowsMixedRealityControllerDataProviderProfile profile)
            : base(name, priority, profile)
        {
            this.profile = profile;
        }

        private readonly WindowsMixedRealityControllerDataProviderProfile profile;

#if WINDOWS_UWP

        /// <summary>
        /// Dictionary to capture all active controllers detected
        /// </summary>
        private readonly Dictionary<uint, IWindowsMixedRealityController> activeControllers = new Dictionary<uint, IWindowsMixedRealityController>();

        /// <summary>
        /// Dictionary capturing cached interaction states from a previous frame.
        /// </summary>
        private readonly Dictionary<uint, SpatialInteractionSourceState> cachedInteractionSourceStates = new Dictionary<uint, SpatialInteractionSourceState>();

        private bool gestureRecognizerEnabled;
        /// <summary>
        /// Enables or disables the gesture recognizer.
        /// </summary>
        /// <remarks>
        /// Automatically disabled navigation recognizer if enabled.
        /// </remarks>
        public bool GestureRecognizerEnabled
        {
            get => gestureRecognizerEnabled;
            set
            {
                gestureRecognizerEnabled = value;
                if (!Application.isPlaying) { return; }

                if (!gestureRecognizerEnabled)
                {
                    gestureRecognizer.CancelPendingGestures();
                }
            }
        }

        private static SpatialGestureSettings defaultGestureSettings = SpatialGestureSettings.Hold | SpatialGestureSettings.ManipulationTranslate
            | SpatialGestureSettings.NavigationX | SpatialGestureSettings.NavigationY | SpatialGestureSettings.NavigationZ;
        /// <summary>
        /// Current Gesture Settings for the GestureRecognizer
        /// </summary>
        public static SpatialGestureSettings DefaultGestureSettings
        {
            get => defaultGestureSettings;
            set
            {
                defaultGestureSettings = value;
                gestureRecognizer.CancelPendingGestures();
                gestureRecognizer = new SpatialGestureRecognizer(DefaultGestureSettings);
            }
        }

        private static SpatialGestureSettings railsNavigationSettings = SpatialGestureSettings.NavigationRailsX | SpatialGestureSettings.NavigationRailsY | SpatialGestureSettings.NavigationRailsZ;
        /// <summary>
        /// Current Navigation Gesture Recognizer Rails Settings.
        /// </summary>
        public static SpatialGestureSettings RailsNavigationSettings
        {
            get => railsNavigationSettings;
            set
            {
                railsNavigationSettings = value;
                if (useRailsNavigation)
                {
                    gestureRecognizer.CancelPendingGestures();
                    gestureRecognizer = new SpatialGestureRecognizer(RailsNavigationSettings);
                }
            }
        }

        private static bool useRailsNavigation = true;
        /// <summary>
        /// Should the Navigation Gesture Recognizer use Rails?
        /// </summary>
        public static bool UseRailsNavigation
        {
            get => useRailsNavigation;
            set
            {
                useRailsNavigation = value;
                gestureRecognizer.CancelPendingGestures();
                gestureRecognizer = new SpatialGestureRecognizer(useRailsNavigation ? RailsNavigationSettings : DefaultGestureSettings);
            }
        }

        private MixedRealityInputAction tapAction = MixedRealityInputAction.None;
        private MixedRealityInputAction doubleTapAction = MixedRealityInputAction.None;
        private MixedRealityInputAction holdAction = MixedRealityInputAction.None;
        private MixedRealityInputAction navigationAction = MixedRealityInputAction.None;
        private MixedRealityInputAction manipulationAction = MixedRealityInputAction.None;

        private static SpatialGestureRecognizer gestureRecognizer;

        private SpatialInteractionManager spatialInteractionManager = null;
        /// <summary>
        /// Gets the <see cref="SpatialInteractionManager"/> instance for the current view.
        /// </summary>
        private SpatialInteractionManager SpatialInteractionManager
        {
            get
            {
                if (spatialInteractionManager == null)
                {
                    UnityEngine.WSA.Application.InvokeOnUIThread(() =>
                    {
                        spatialInteractionManager = SpatialInteractionManager.GetForCurrentView();
                    }, true);
                }

                return spatialInteractionManager;
            }
        }

        #region IMixedRealityService Interface

        public override void Initialize()
        {
            base.Initialize();

            gestureRecognizer = new SpatialGestureRecognizer(profile.ManipulationGestureSettings);
        }

        /// <inheritdoc/>
        public override void Enable()
        {
            if (!Application.isPlaying) { return; }

            gestureRecognizer.Tapped += GestureRecognizer_Tapped;
            gestureRecognizer.HoldStarted += GestureRecognizer_HoldStarted;
            gestureRecognizer.HoldCompleted += GestureRecognizer_HoldCompleted;
            gestureRecognizer.HoldCanceled += GestureRecognizer_HoldCanceled;

            gestureRecognizer.ManipulationStarted += GestureRecognizer_ManipulationStarted;
            gestureRecognizer.ManipulationUpdated += GestureRecognizer_ManipulationUpdated;
            gestureRecognizer.ManipulationCompleted += GestureRecognizer_ManipulationCompleted;
            gestureRecognizer.ManipulationCanceled += GestureRecognizer_ManipulationCanceled;

            gestureRecognizer.NavigationStarted += GestureRecognizer_NavigationStarted;
            gestureRecognizer.NavigationUpdated += GestureRecognizer_NavigationUpdated;
            gestureRecognizer.NavigationCompleted += GestureRecognizer_NavigationCompleted;
            gestureRecognizer.NavigationCanceled += GestureRecognizer_NavigationCanceled;

            if (MixedRealityToolkit.Instance.ActiveProfile.IsInputSystemEnabled &&
                MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.GesturesProfile != null)
            {
                MixedRealityGesturesProfile gestureProfile = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.GesturesProfile;
                DefaultGestureSettings = profile.ManipulationGestureSettings;
                NavigationSettings = profile.NavigationGestureSettings;
                RailsNavigationSettings = profile.RailsNavigationGestureSettings;
                UseRailsNavigation = profile.UseRailsNavigation;

                for (int i = 0; i < gestureProfile.Gestures.Length; i++)
                {
                    var gesture = gestureProfile.Gestures[i];

                    switch (gesture.GestureType)
                    {
                        case GestureInputType.Hold:
                            holdAction = gesture.Action;
                            break;
                        case GestureInputType.Manipulation:
                            manipulationAction = gesture.Action;
                            break;
                        case GestureInputType.Navigation:
                            navigationAction = gesture.Action;
                            break;
                        case GestureInputType.Tap:
                            tapAction = gesture.Action;
                            break;
                        case GestureInputType.DoubleTap:
                            doubleTapAction = gesture.Action;
                            break;
                    }
                }
            }

            SpatialInteractionManager.SourceDetected += SpatialInteractionManager_SourceDetected;
            SpatialInteractionManager.SourceLost += SpatialInteractionManager_SourceLost;
            SpatialInteractionManager.InteractionDetected += SpatialInteractionManager_InteractionDetected;

            // NOTE: We update the source state data, in case an app wants to query it on source detected.
            IReadOnlyList<SpatialInteractionSourceState> sources = GetCurrentSources();
            for (int i = 0; i < sources.Count; i++)
            {
                SpatialInteractionSourceState sourceState = sources[i];
                SpatialInteractionSource spatialInteractionSource = sourceState.Source;

                if (!TryGetController(spatialInteractionSource, out IWindowsMixedRealityController existingController))
                {
                    IWindowsMixedRealityController controller = CreateController(spatialInteractionSource);
                    if (controller != null)
                    {
                        MixedRealityToolkit.InputSystem?.RaiseSourceDetected(controller.InputSource, controller);
                        controller.UpdateController(sourceState);
                    }
                }
            }

            if (MixedRealityToolkit.Instance.ActiveProfile.IsInputSystemEnabled &&
                MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.GesturesProfile != null &&
                profile.GestureRecognitionStartBehaviour == AutoStartBehavior.AutoStart)
            {
                GestureRecognizerEnabled = true;
            }
        }

        /// <inheritdoc/>
        public override void Update()
        {
            base.Update();

            // Update existing controllers or create a new one if needed.
            IReadOnlyList<SpatialInteractionSourceState> sources = GetCurrentSources();
            for (int i = 0; i < sources.Count; i++)
            {
                SpatialInteractionSourceState sourceState = sources[i];
                SpatialInteractionSource spatialInteractionSource = sourceState.Source;

                // If we already have a controller created for this source, update it.
                if (TryGetController(spatialInteractionSource, out IWindowsMixedRealityController existingController))
                {
                    existingController.UpdateController(sourceState);
                }
                else
                {
                    // Try and crate a new controller if not.
                    IWindowsMixedRealityController controller = CreateController(spatialInteractionSource);
                    if (controller != null)
                    {
                        MixedRealityToolkit.InputSystem?.RaiseSourceDetected(controller.InputSource, controller);
                        controller.UpdateController(sourceState);
                    }
                }

                // Update cached state for this interactino source.
                if (cachedInteractionSourceStates.ContainsKey(spatialInteractionSource.Id))
                {
                    cachedInteractionSourceStates[spatialInteractionSource.Id] = sourceState;
                }
                else
                {
                    cachedInteractionSourceStates.Add(spatialInteractionSource.Id, sourceState);
                }
            }

            // We need to cleanup any controllers, that are not detected / tracked anymore as well.
            foreach (var controllerRegistry in activeControllers)
            {
                uint id = controllerRegistry.Key;
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources[i].Source.Id.Equals(id))
                    {
                        continue;
                    }

                    // This controller is not in the up-to-date sources list,
                    // so we need to remove it.
                    RemoveController(cachedInteractionSourceStates[id], true);
                    cachedInteractionSourceStates.Remove(id);
                }
            }
        }

        /// <inheritdoc/>
        public override void Disable()
        {
            base.Disable();

            gestureRecognizer.Tapped -= GestureRecognizer_Tapped;
            gestureRecognizer.HoldStarted -= GestureRecognizer_HoldStarted;
            gestureRecognizer.HoldCompleted -= GestureRecognizer_HoldCompleted;
            gestureRecognizer.HoldCanceled -= GestureRecognizer_HoldCanceled;

            gestureRecognizer.ManipulationStarted -= GestureRecognizer_ManipulationStarted;
            gestureRecognizer.ManipulationUpdated -= GestureRecognizer_ManipulationUpdated;
            gestureRecognizer.ManipulationCompleted -= GestureRecognizer_ManipulationCompleted;
            gestureRecognizer.ManipulationCanceled -= GestureRecognizer_ManipulationCanceled;

            gestureRecognizer.NavigationStarted -= GestureRecognizer_NavigationStarted;
            gestureRecognizer.NavigationUpdated -= GestureRecognizer_NavigationUpdated;
            gestureRecognizer.NavigationCompleted -= GestureRecognizer_NavigationCompleted;
            gestureRecognizer.NavigationCanceled -= GestureRecognizer_NavigationCanceled;

            SpatialInteractionManager.SourceDetected -= SpatialInteractionManager_SourceDetected;
            SpatialInteractionManager.SourceLost -= SpatialInteractionManager_SourceLost;
            SpatialInteractionManager.InteractionDetected -= SpatialInteractionManager_InteractionDetected;

            IReadOnlyList<SpatialInteractionSourceState> sources = GetCurrentSources();
            for (int i = 0; i < sources.Count; i++)
            {
                SpatialInteractionSourceState sourceState = sources[i];
                RemoveController(sourceState, false);
            }

            foreach (var cachedState in cachedInteractionSourceStates)
            {
                RemoveController(cachedState.Value);
            }

            cachedInteractionSourceStates.Clear();
        }

        #endregion IMixedRealityService Interface

        #region Controller Management

        /// <summary>
        /// Reads currently detected input sources by the current <see cref="SpatialInteractionManager"/> instance.
        /// </summary>
        /// <returns>List of sources. Can be null.</returns>
        private IReadOnlyList<SpatialInteractionSourceState> GetCurrentSources()
        {
            // Articulated hand support is only present in the 18362 version and beyond Windows
            // SDK (which contains the V8 drop of the Universal API Contract). In particular,
            // the HandPose related APIs are only present on this version and above.
            if (WindowsApiChecker.UniversalApiContractV8_IsAvailable && SpatialInteractionManager != null)
            {
                PerceptionTimestamp perceptionTimestamp = PerceptionTimestampHelper.FromHistoricalTargetTime(DateTimeOffset.Now);
                IReadOnlyList<SpatialInteractionSourceState> sources = SpatialInteractionManager.GetDetectedSourcesAtTimestamp(perceptionTimestamp);

                return sources;
            }

            return null;
        }

        /// <summary>
        /// Checks whether a <see cref="IWindowsMixedRealityController"/> has already been created and registered
        /// for a given <see cref="SpatialInteractionSource"/>.
        /// </summary>
        /// <param name="spatialInteractionSource">Input source to lookup the controller for.</param>
        /// <param name="windowsMixedRealityController">Reference to found controller, if existing.</param>
        /// <returns>True, if the controller is registered and alive.</returns>
        private bool TryGetController(SpatialInteractionSource spatialInteractionSource, out IWindowsMixedRealityController windowsMixedRealityController)
        {
            if (activeControllers.ContainsKey(spatialInteractionSource.Id))
            {
                windowsMixedRealityController = activeControllers[spatialInteractionSource.Id];
                if (windowsMixedRealityController == null)
                {
                    Debug.LogError($"Controller {spatialInteractionSource.Id} was not properly unregistered or unexpectedly destroyed.");
                    activeControllers.Remove(spatialInteractionSource.Id);
                    return false;
                }

                return true;
            }

            windowsMixedRealityController = null;
            return false;
        }

        /// <summary>
        /// Creates the controller for a new device and registers it.
        /// </summary>
        /// <param name="spatialInteractionSource">Source State provided by the SDK.</param>
        /// <returns>New controller input source.</returns>
        private IWindowsMixedRealityController CreateController(SpatialInteractionSource spatialInteractionSource)
        {
            // We are creating a new controller for the source, determine the type of controller to use.
            Type controllerType = spatialInteractionSource.Kind.ToControllerType();
            if (controllerType == null)
            {
                Debug.LogError($"Windows Mixed Reality controller type {spatialInteractionSource.Kind} not supported.");
                return null;
            }

            // Ready to create the controller intance.
            Handedness controllingHand = spatialInteractionSource.Handedness.ToHandedness();
            IMixedRealityPointer[] pointers = spatialInteractionSource.IsPointingSupported ? RequestPointers(controllerType, controllingHand) : null;
            string nameModifier = controllingHand == Handedness.None ? spatialInteractionSource.Kind.ToString() : controllingHand.ToString();
            IMixedRealityInputSource inputSource = MixedRealityToolkit.InputSystem?.RequestNewGenericInputSource($"Mixed Reality Controller {nameModifier}", pointers);
            IWindowsMixedRealityController detectedController = Activator.CreateInstance(controllerType, TrackingState.NotApplicable, controllingHand, inputSource, null) as IWindowsMixedRealityController;

            if (!detectedController.SetupConfiguration(controllerType))
            {
                // Controller failed to be setup correctly.
                // Return null so we don't raise the source detected.
                return null;
            }

            TryRenderControllerModel(spatialInteractionSource, detectedController);

            for (int i = 0; i < detectedController.InputSource?.Pointers?.Length; i++)
            {
                detectedController.InputSource.Pointers[i].Controller = detectedController;
            }

            activeControllers.Add(spatialInteractionSource.Id, detectedController);
            return detectedController;
        }

        private static async void TryRenderControllerModel(SpatialInteractionSource spatialInteractionSource, IWindowsMixedRealityController controller)
        {
            if (!UnityEngine.XR.WSA.HolographicSettings.IsDisplayOpaque) { return; }
            IRandomAccessStreamWithContentType stream = null;

            if (!WindowsApiChecker.UniversalApiContractV5_IsAvailable) { return; }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, DispatchedHandler);

            async void DispatchedHandler()
            {
                byte[] glbModelData = null;
                var sources = SpatialInteractionManager
                    .GetForCurrentView()
                    .GetDetectedSourcesAtTimestamp(
                        PerceptionTimestampHelper.FromHistoricalTargetTime(DateTimeOffset.Now));

                for (var i = 0; i < sources?.Count; i++)
                {
                    if (sources[i].Source.Id.Equals(spatialInteractionSource.Id))
                    {
                        stream = await sources[i].Source.Controller.TryGetRenderableModelAsync();
                        break;
                    }
                }

                if (stream != null)
                {
                    glbModelData = new byte[stream.Size];

                    using (var reader = new DataReader(stream))
                    {
                        await reader.LoadAsync((uint)stream.Size);
                        reader.ReadBytes(glbModelData);
                    }

                    stream.Dispose();
                }
                else
                {
                    Debug.LogError("Failed to load model data!");
                }

                // This really isn't an error, we actually can call TryRenderControllerModelAsync here.
                controller.TryRenderControllerModel(controller.GetType(), glbModelData, spatialInteractionSource.Kind == SpatialInteractionSourceKind.Hand);
            }
        }

        /// <summary>
        /// Removes the selected controller from the active store.
        /// </summary>
        /// <param name="spatialInteractionSourceState">Source State provided by the SDK to remove.</param>
        /// <param name="clearFromRegistry">Should the controller be removed from the registry?</param>
        private void RemoveController(SpatialInteractionSourceState spatialInteractionSourceState, bool clearFromRegistry = true)
        {
            if (TryGetController(spatialInteractionSourceState.Source, out IWindowsMixedRealityController controller))
            {
                MixedRealityToolkit.InputSystem?.RaiseSourceLost(controller.InputSource, controller);
                RemoveController(controller);

                if (clearFromRegistry)
                {
                    activeControllers.Remove(spatialInteractionSourceState.Source.Id);
                }
            }
        }

        #endregion Controller Management

        #region SpatialInteractionManager Events

        /// <summary>
        /// SDK Interaction Source Detected Event handler.
        /// </summary>
        /// <param name="sender">The interaction manager raising the event.</param>
        /// <param name="args">SDK source detected event arguments.</param>
        private void SpatialInteractionManager_SourceDetected(SpatialInteractionManager sender, SpatialInteractionSourceEventArgs args)
        {
            if (TryGetController(args.State.Source, out IWindowsMixedRealityController existingController))
            {
                existingController.UpdateController(args.State);
            }
            else
            {
                IWindowsMixedRealityController controller = CreateController(args.State.Source);
                if (controller != null)
                {
                    MixedRealityToolkit.InputSystem?.RaiseSourceDetected(controller.InputSource, controller);
                    controller.UpdateController(args.State);
                }
            }
        }

        /// <summary>
        /// SDK Interaction Source Lost Event handler.
        /// </summary>
        /// <param name="sender">The interaction manager raising the event.</param>
        /// <param name="args">SDK source updated event arguments.</param>
        private void SpatialInteractionManager_SourceLost(SpatialInteractionManager sender, SpatialInteractionSourceEventArgs args)
        {
            RemoveController(args.State);
        }

        private void SpatialInteractionManager_InteractionDetected(SpatialInteractionManager sender, SpatialInteractionDetectedEventArgs args)
        {
            if (gestureRecognizerEnabled)
            {
                gestureRecognizer.CancelPendingGestures();
                gestureRecognizer.CaptureInteraction(args.Interaction);
            }
        }

        #endregion SpatialInteractionManager Events

        #region Gesture Recognizer Events

        private void GestureRecognizer_Tapped(SpatialGestureRecognizer sender, SpatialTappedEventArgs args)
        {
            if (TryGetController())
            {
                if (args.TapCount == 1)
                {
                    MixedRealityToolkit.InputSystem?.RaiseGestureStarted(controller, tapAction);
                    MixedRealityToolkit.InputSystem?.RaiseGestureCompleted(controller, tapAction);
                }
                else if (args.TapCount == 2)
                {
                    MixedRealityToolkit.InputSystem?.RaiseGestureStarted(controller, doubleTapAction);
                    MixedRealityToolkit.InputSystem?.RaiseGestureCompleted(controller, doubleTapAction);
                }
            }
        }

        private void GestureRecognizer_HoldStarted(SpatialGestureRecognizer sender, SpatialHoldStartedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem?.RaiseGestureStarted(controller, holdAction);
            }
        }

        private void GestureRecognizer_HoldCompleted(SpatialGestureRecognizer sender, SpatialHoldCompletedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureCompleted(controller, holdAction);
            }
        }

        private void GestureRecognizer_HoldCanceled(SpatialGestureRecognizer sender, SpatialHoldCanceledEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureCanceled(controller, holdAction);
            }
        }

        private void GestureRecognizer_ManipulationStarted(SpatialGestureRecognizer sender, SpatialManipulationStartedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureStarted(controller, manipulationAction);
            }
        }

        private void GestureRecognizer_ManipulationUpdated(SpatialGestureRecognizer sender, SpatialManipulationUpdatedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureUpdated(controller, manipulationAction, args.TryGetCumulativeDelta(SpatialCoordinateSystem).Translation);
            }
        }

        private void GestureRecognizer_ManipulationCompleted(SpatialGestureRecognizer sender, SpatialManipulationCompletedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureCompleted(controller, manipulationAction, args.TryGetCumulativeDelta(SpatialCoordinateSystem).Translation);
            }
        }

        private void GestureRecognizer_ManipulationCanceled(SpatialGestureRecognizer sender, SpatialManipulationCanceledEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureCanceled(controller, manipulationAction);
            }
        }

        #endregion Gesture Recognizer Events

        #region Navigation Recognizer Events

        private void GestureRecognizer_NavigationStarted(SpatialGestureRecognizer sender, SpatialNavigationStartedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureStarted(controller, navigationAction);
            }
        }

        private void GestureRecognizer_NavigationUpdated(SpatialGestureRecognizer sender, SpatialNavigationUpdatedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureUpdated(controller, navigationAction, args.NormalizedOffset);
            }
        }

        private void GestureRecognizer_NavigationCompleted(SpatialGestureRecognizer sender, SpatialNavigationCompletedEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureCompleted(controller, navigationAction, args.NormalizedOffset);
            }
        }

        private void GestureRecognizer_NavigationCanceled(SpatialGestureRecognizer sender, SpatialNavigationCanceledEventArgs args)
        {
            if (TryGetController())
            {
                MixedRealityToolkit.InputSystem.RaiseGestureCanceled(controller, navigationAction);
            }
        }

        #endregion Navigation Recognizer Events

#endif // WINDOWS_UWP
    }
}