// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XRTK.Definitions.Devices;
using XRTK.Definitions.InputSystem;
using XRTK.Definitions.Utilities;
using XRTK.Interfaces.InputSystem;
using XRTK.Providers.Controllers;
using XRTK.WindowsMixedReality.Interfaces.Providers.Controllers;

#if WINDOWS_UWP
using Windows.Perception.People;
using Windows.UI.Input.Spatial;
using UnityEngine;
using UnityEngine.XR.WSA.Input;
using XRTK.Services;
using XRTK.Extensions;
using XRTK.WindowsMixedReality.Extensions;
using XRTK.WindowsMixedReality.Utilities;
#endif // WINDOWS_UWP

namespace XRTK.WindowsMixedReality.Controllers
{
    /// <summary>
    /// A Windows Mixed Reality Controller Instance.
    /// </summary>
    public class WindowsMixedRealityMotionController : BaseController, IWindowsMixedRealityController
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="trackingState"></param>
        /// <param name="controllerHandedness"></param>
        /// <param name="inputSource"></param>
        /// <param name="interactions"></param>
        public WindowsMixedRealityMotionController(TrackingState trackingState, Handedness controllerHandedness, IMixedRealityInputSource inputSource = null, MixedRealityInteractionMapping[] interactions = null)
                : base(trackingState, controllerHandedness, inputSource, interactions)
        {
        }

        /// <summary>
        /// The Windows Mixed Reality Controller default interactions.
        /// </summary>
        /// <remarks>A single interaction mapping works for both left and right controllers.</remarks>
        public override MixedRealityInteractionMapping[] DefaultInteractions => new[]
        {
            new MixedRealityInteractionMapping(0, "Spatial Pointer", AxisType.SixDof, DeviceInputType.SpatialPointer, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(1, "Spatial Grip", AxisType.SixDof, DeviceInputType.SpatialGrip, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(2, "Grip Press", AxisType.SingleAxis, DeviceInputType.TriggerPress, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(3, "Trigger Position", AxisType.SingleAxis, DeviceInputType.Trigger, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(4, "Trigger Touched", AxisType.Digital, DeviceInputType.TriggerTouch, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(5, "Trigger Press (Select)", AxisType.Digital, DeviceInputType.Select, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(6, "Touchpad Position", AxisType.DualAxis, DeviceInputType.Touchpad, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(7, "Touchpad Touch", AxisType.Digital, DeviceInputType.TouchpadTouch, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(8, "Touchpad Press", AxisType.Digital, DeviceInputType.TouchpadPress, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(9, "Menu Press", AxisType.Digital, DeviceInputType.Menu, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(10, "Thumbstick Position", AxisType.DualAxis, DeviceInputType.ThumbStick, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(11, "Thumbstick Press", AxisType.Digital, DeviceInputType.ThumbStickPress, MixedRealityInputAction.None),
        };

        /// <inheritdoc />
        public override MixedRealityInteractionMapping[] DefaultLeftHandedInteractions => DefaultInteractions;

        /// <inheritdoc />
        public override MixedRealityInteractionMapping[] DefaultRightHandedInteractions => DefaultInteractions;

        /// <inheritdoc />
        public override void SetupDefaultInteractions(Handedness controllerHandedness)
        {
            AssignControllerMappings(DefaultInteractions);
        }

#if WINDOWS_UWP

        /// <summary>
        /// The last updated source state reading for this Windows Mixed Reality Controller.
        /// </summary>
        public SpatialInteractionSourceState LastSourceStateReading { get; private set; }

        private Vector3 currentControllerPosition = Vector3.zero;
        private Quaternion currentControllerRotation = Quaternion.identity;
        private MixedRealityPose lastControllerPose = MixedRealityPose.ZeroIdentity;
        private MixedRealityPose currentControllerPose = MixedRealityPose.ZeroIdentity;

        private Vector3 currentPointerPosition = Vector3.zero;
        private Quaternion currentPointerRotation = Quaternion.identity;
        private MixedRealityPose currentPointerPose = MixedRealityPose.ZeroIdentity;

        private Vector3 currentGripPosition = Vector3.zero;
        private Quaternion currentGripRotation = Quaternion.identity;
        private MixedRealityPose currentGripPose = MixedRealityPose.ZeroIdentity;

        /// <inheritdoc />
        public void UpdateController(SpatialInteractionSourceState spatialInteractionSourceState)
        {
            if (!Enabled) { return; }

            base.UpdateController();

            UpdateControllerData(spatialInteractionSourceState);

            if (Interactions == null)
            {
                Debug.LogError($"No interaction configuration for {GetType().Name} {ControllerHandedness}");
                Enabled = false;
            }

            for (int i = 0; i < Interactions?.Length; i++)
            {
                var interactionMapping = Interactions[i];

                switch (interactionMapping.InputType)
                {
                    case DeviceInputType.None:
                        break;
                    case DeviceInputType.SpatialPointer:
                        UpdatePointerData(spatialInteractionSourceState, interactionMapping);
                        break;
                    case DeviceInputType.Select:
                    case DeviceInputType.Trigger:
                    case DeviceInputType.TriggerTouch:
                    case DeviceInputType.TriggerPress:
                        UpdateTriggerData(spatialInteractionSourceState, interactionMapping);
                        break;
                    case DeviceInputType.SpatialGrip:
                        UpdateGripData(spatialInteractionSourceState, interactionMapping);
                        break;
                    case DeviceInputType.ThumbStick:
                    case DeviceInputType.ThumbStickPress:
                        UpdateThumbStickData(spatialInteractionSourceState, interactionMapping);
                        break;
                    case DeviceInputType.Touchpad:
                    case DeviceInputType.TouchpadTouch:
                    case DeviceInputType.TouchpadPress:
                        UpdateTouchPadData(spatialInteractionSourceState, interactionMapping);
                        break;
                    case DeviceInputType.Menu:
                        UpdateMenuData(spatialInteractionSourceState, interactionMapping);
                        break;
                    default:
                        Debug.LogError($"Input [{interactionMapping.InputType}] is not handled for this controller [{GetType().Name}]");
                        Enabled = false;
                        break;
                }

                interactionMapping.RaiseInputAction(InputSource, ControllerHandedness);
            }

            LastSourceStateReading = spatialInteractionSourceState;
        }

        /// <summary>
        /// Update the "Controller" input from the device.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        private void UpdateControllerData(SpatialInteractionSourceState spatialInteractionSourceState)
        {
            var lastState = TrackingState;
            var sourceKind = spatialInteractionSourceState.Source.Kind;

            lastControllerPose = currentControllerPose;

            if (sourceKind == SpatialInteractionSourceKind.Hand ||
               (sourceKind == SpatialInteractionSourceKind.Controller && spatialInteractionSourceState.Source.IsPointingSupported))
            {
                // The source is either a hand or a controller that supports pointing.
                // We can now check for position and rotation.
                spatialInteractionSourceState.
                IsPositionAvailable = spatialInteractionSourceState.sourcePose.TryGetPosition(out currentControllerPosition);

                if (IsPositionAvailable)
                {
                    IsPositionApproximate = (spatialInteractionSourceState.sourcePose.positionAccuracy == InteractionSourcePositionAccuracy.Approximate);
                }
                else
                {
                    IsPositionApproximate = false;
                }

                IsRotationAvailable = spatialInteractionSourceState.sourcePose.TryGetRotation(out currentControllerRotation);

                // Devices are considered tracked if we receive position OR rotation data from the sensors.
                TrackingState = (IsPositionAvailable || IsRotationAvailable) ? TrackingState.Tracked : TrackingState.NotTracked;
            }
            else
            {
                // The input source does not support tracking.
                TrackingState = TrackingState.NotApplicable;
            }

            currentControllerPose.Position = currentControllerPosition;
            currentControllerPose.Rotation = currentControllerRotation;

            // Raise input system events if it is enabled.
            if (lastState != TrackingState)
            {
                MixedRealityToolkit.InputSystem?.RaiseSourceTrackingStateChanged(InputSource, this, TrackingState);
            }

            if (TrackingState == TrackingState.Tracked && lastControllerPose != currentControllerPose)
            {
                if (IsPositionAvailable && IsRotationAvailable)
                {
                    MixedRealityToolkit.InputSystem?.RaiseSourcePoseChanged(InputSource, this, currentControllerPose);
                }
                else if (IsPositionAvailable && !IsRotationAvailable)
                {
                    MixedRealityToolkit.InputSystem?.RaiseSourcePositionChanged(InputSource, this, currentControllerPosition);
                }
                else if (!IsPositionAvailable && IsRotationAvailable)
                {
                    MixedRealityToolkit.InputSystem?.RaiseSourceRotationChanged(InputSource, this, currentControllerRotation);
                }
            }
        }

        /// <summary>
        /// Update the "Spatial Pointer" input from the device.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        /// <param name="interactionMapping">The interaction mapping to update.</param>
        private void UpdatePointerData(SpatialInteractionSourceState spatialInteractionSourceState, MixedRealityInteractionMapping interactionMapping)
        {
            if (spatialInteractionSourceState.Source.IsPointingSupported)
            {
                SpatialPointerInteractionSourcePose spatialPointerPose = spatialInteractionSourceState.TryGetPointerPose(WindowsMixedRealityUtilities.SpatialCoordinateSystem).TryGetInteractionSourcePose(spatialInteractionSourceState.Source);
                currentPointerPose.Position = spatialPointerPose.Position.ToUnity();
                currentPointerPose.Rotation = spatialPointerPose.Orientation.ToUnity();
            }
            else
            {
                HeadPose headPose = spatialInteractionSourceState.TryGetPointerPose(WindowsMixedRealityUtilities.SpatialCoordinateSystem).Head;
                currentPointerPose.Position = headPose.Position.ToUnity();
                currentPointerPose.Rotation = Quaternion.LookRotation(headPose.ForwardDirection.ToUnity(), headPose.UpDirection.ToUnity());
            }

            interactionMapping.PoseData = currentPointerPose;
        }

        /// <summary>
        /// Update the "Spatial Grip" input from the device.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        /// <param name="interactionMapping">The interaction mapping to update.</param>
        private void UpdateGripData(SpatialInteractionSourceState spatialInteractionSourceState, MixedRealityInteractionMapping interactionMapping)
        {
            switch (interactionMapping.AxisType)
            {
                case AxisType.SixDof:
                    {
                        //SpatialPointerInteractionSourcePose spatialPointerPose = spatialInteractionSourceState.TryGetPointerPose(WindowsMixedRealityUtilities.SpatialCoordinateSystem).TryGetInteractionSourcePose(spatialInteractionSourceState.Source);
                        spatialInteractionSourceState.sourcePose.TryGetPosition(out currentGripPosition, InteractionSourceNode.Grip);
                        spatialInteractionSourceState.sourcePose.TryGetRotation(out currentGripRotation, InteractionSourceNode.Grip);

                        var cameraRig = MixedRealityToolkit.CameraSystem?.CameraRig;

                        if (cameraRig != null &&
                            cameraRig.PlayspaceTransform != null)
                        {
                            currentGripPose.Position = cameraRig.PlayspaceTransform.TransformPoint(currentGripPosition);
                            currentGripPose.Rotation = Quaternion.Euler(cameraRig.PlayspaceTransform.TransformDirection(currentGripRotation.eulerAngles));
                        }
                        else
                        {
                            currentGripPose.Position = currentGripPosition;
                            currentGripPose.Rotation = currentGripRotation;
                        }

                        interactionMapping.PoseData = currentGripPose;
                    }
                    break;
            }
        }

        /// <summary>
        /// Update the Touchpad input from the device.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        /// <param name="interactionMapping">The interaction mapping to update.</param>
        private void UpdateTouchPadData(SpatialInteractionSourceState spatialInteractionSourceState, MixedRealityInteractionMapping interactionMapping)
        {
            switch (interactionMapping.InputType)
            {
                case DeviceInputType.TouchpadTouch:
                    {
                        interactionMapping.BoolData = spatialInteractionSourceState.ControllerProperties.IsTouchpadTouched;
                        break;
                    }
                case DeviceInputType.TouchpadPress:
                    {
                        interactionMapping.BoolData = spatialInteractionSourceState.ControllerProperties.IsTouchpadPressed;
                        break;
                    }
                case DeviceInputType.Touchpad:
                    {
                        interactionMapping.Vector2Data = new Vector2((float)spatialInteractionSourceState.ControllerProperties.TouchpadX, (float)spatialInteractionSourceState.ControllerProperties.TouchpadY);
                        break;
                    }
            }
        }

        /// <summary>
        /// Update the Thumbstick input from the device.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        /// <param name="interactionMapping">The interaction mapping to update.</param>
        private void UpdateThumbStickData(SpatialInteractionSourceState spatialInteractionSourceState, MixedRealityInteractionMapping interactionMapping)
        {
            switch (interactionMapping.InputType)
            {
                case DeviceInputType.ThumbStickPress:
                    {
                        interactionMapping.BoolData = spatialInteractionSourceState.ControllerProperties.IsThumbstickPressed;
                        break;
                    }
                case DeviceInputType.ThumbStick:
                    {
                        interactionMapping.Vector2Data = new Vector2((float)spatialInteractionSourceState.ControllerProperties.ThumbstickX, (float)spatialInteractionSourceState.ControllerProperties.ThumbstickY);
                        break;
                    }
            }
        }

        /// <summary>
        /// Update the Trigger input from the device.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        /// <param name="interactionMapping">The interaction mapping to update.</param>
        private void UpdateTriggerData(SpatialInteractionSourceState spatialInteractionSourceState, MixedRealityInteractionMapping interactionMapping)
        {
            switch (interactionMapping.InputType)
            {
                case DeviceInputType.TriggerPress:
                    interactionMapping.BoolData = spatialInteractionSourceState.IsGrasped;
                    break;
                case DeviceInputType.Select:
                    {
                        bool selectPressed = spatialInteractionSourceState.IsSelectPressed;

                        // BEGIN WORKAROUND: Unity issue #1033526
                        // See https://issuetracker.unity3d.com/issues/hololens-interactionsourcestate-dot-selectpressed-is-false-when-air-tap-and-hold
                        // Bug was discovered May 2018 and still exists as of today Feb 2019 in version 2018.3.4f1, timeline for fix is unknown
                        // The bug only affects the development workflow via Holographic Remoting or Simulation
                        if (spatialInteractionSourceState.Source.Kind == SpatialInteractionSourceKind.Hand)
                        {
                            Debug.Assert(!(UnityEngine.XR.WSA.HolographicRemoting.ConnectionState == UnityEngine.XR.WSA.HolographicStreamerConnectionState.Connected
                                           && selectPressed),
                                         "Unity issue #1033526 seems to have been resolved. Please remove this ugly workaround!");

                            // This workaround is safe as long as all these assumptions hold:
                            Debug.Assert(!spatialInteractionSourceState.Source.IsGraspSupported);
                            Debug.Assert(!spatialInteractionSourceState.Source.IsMenuSupported);
                            Debug.Assert(!spatialInteractionSourceState.Source.IsPointingSupported);
                            Debug.Assert(!spatialInteractionSourceState.Source.Controller.HasThumbstick);
                            Debug.Assert(!spatialInteractionSourceState.Source.Controller.HasTouchpad);

                            selectPressed = spatialInteractionSourceState.IsPressed;
                        }
                        // END WORKAROUND: Unity issue #1033526

                        interactionMapping.BoolData = selectPressed;
                        break;
                    }
                case DeviceInputType.Trigger:
                    {
                        interactionMapping.FloatData = (float)spatialInteractionSourceState.SelectPressedValue;
                        break;
                    }
                case DeviceInputType.TriggerTouch:
                    {
                        interactionMapping.BoolData = spatialInteractionSourceState.SelectPressedValue > 0;
                        break;
                    }
            }
        }

        /// <summary>
        /// Update the Menu button state.
        /// </summary>
        /// <param name="spatialInteractionSourceState">The InteractionSourceState retrieved from the platform.</param>
        /// <param name="interactionMapping">The interaction mapping to update.</param>
        private void UpdateMenuData(SpatialInteractionSourceState spatialInteractionSourceState, MixedRealityInteractionMapping interactionMapping)
        {
            interactionMapping.BoolData = spatialInteractionSourceState.IsMenuPressed;
        }

#endif // WINDOWS_UWP
    }
}