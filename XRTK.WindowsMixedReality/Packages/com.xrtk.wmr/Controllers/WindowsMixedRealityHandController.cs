﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XRTK.Definitions.Devices;
using XRTK.Definitions.Utilities;
using XRTK.Interfaces.InputSystem;
using XRTK.Providers.Controllers.Hands;

namespace XRTK.WindowsMixedReality.Controllers
{
    /// <summary>
    /// The default hand controller implementation for the Windows Universal platform.
    /// </summary>
    public class WindowsMixedRealityHandController : BaseHandController
    {
        public WindowsMixedRealityHandController(TrackingState trackingState, Handedness controllerHandedness, IMixedRealityInputSource inputSource = null, MixedRealityInteractionMapping[] interactions = null)
            : base(trackingState, controllerHandedness, inputSource, interactions)
        {
        }
    }
}