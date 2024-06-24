﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Management.Infrastructure;
using Serilog;
using Windows.Foundation.Metadata;

namespace DevHome.Common.Helpers;

// States based on InstallState value in Win32_OptionalFeature
// See: https://learn.microsoft.com/windows/win32/cimwin32prov/win32-optionalfeature
public enum FeatureAvailabilityKind
{
    Enabled,
    Disabled,
    Absent,
    Unknown,
}

public static class ManagementInfrastructureHelper
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", nameof(ManagementInfrastructureHelper));

    public static readonly Dictionary<string, string> ExtensionToFeatureNameMap = new()
    {
        { CommonConstants.HyperVExtensionClassId, CommonConstants.HyperVWindowsOptionalFeatureName },
    };

    public static FeatureAvailabilityKind GetWindowsFeatureAvailability(string? featureName)
    {
        try
        {
            if (featureName == null)
            {
                return FeatureAvailabilityKind.Unknown;
            }

            // use the local session
            using var session = CimSession.Create(null);

            // There will only be one feature returned by the query
            foreach (var featureInstance in session.QueryInstances("root\\cimv2", "WQL", $"SELECT * FROM Win32_OptionalFeature WHERE Name = '{featureName}'"))
            {
                if (featureInstance?.CimInstanceProperties["InstallState"].Value is uint installState)
                {
                    var featureAvailability = GetAvailabilityKindFromState(installState);

                    _log.Information($"Found feature: '{featureName}' with enablement state: '{featureAvailability}'");
                    return featureAvailability;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error attempting to get the {featureName} feature state");
        }

        _log.Information($"Unable to get state of {featureName} feature");
        return FeatureAvailabilityKind.Unknown;
    }

    private static FeatureAvailabilityKind GetAvailabilityKindFromState(uint state)
    {
        switch (state)
        {
            case 1:
                return FeatureAvailabilityKind.Enabled;
            case 2:
                return FeatureAvailabilityKind.Disabled;
            case 3:
                return FeatureAvailabilityKind.Absent;
            default:
                return FeatureAvailabilityKind.Unknown;
        }
    }

    /// <summary>
    /// Gets a boolean indicating whether the Windows optional feature that an extension relies on
    /// is available on the machine.
    /// </summary>
    /// <param name="featureName">The name of the Windows optional feature that will be queried</param>
    /// <returns>True when the feature is either in the enabled or disabled state. False otherwise.</returns>
    public static bool IsWindowsOptionalFeatureAvailable(string featureName)
    {
        var availability = GetWindowsFeatureAvailability(featureName);

        _log.Information($"Feature {featureName} is currently '{availability}'");
        return (availability == FeatureAvailabilityKind.Enabled) || (availability == FeatureAvailabilityKind.Disabled);
    }

    /// <summary>
    /// Gets a boolean indicating whether the Windows optional feature that an extension relies on
    /// is available on the machine.
    /// </summary>
    /// <param name="extensionClassId">The class Id of the out of proc extension object</param>
    /// <returns>
    /// True only when one of the following is met:
    /// 1. The classId is not from an extension that Dev Home internally knows about.
    /// 2. The classId is from an extension Dev Home internally knows about and the feature is either enabled or disabled.
    /// </returns>
    public static bool IsWindowsOptionalFeatureAvailableForExtension(string extensionClassId)
    {
        ExtensionToFeatureNameMap.TryGetValue(extensionClassId, out var featureName);

        if (featureName == null)
        {
            // This isn't an internal Dev Home extension that we know about, so don't try to disable it.
            return true;
        }

        return IsWindowsOptionalFeatureAvailable(featureName);
    }
}
