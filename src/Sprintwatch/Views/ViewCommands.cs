using System;
using Sprintwatch.Services;

namespace Sprintwatch.Views;

/// <summary>
/// Everything the main view can ask the window to do, so the view itself never touches the
/// configuration, a service or the wall clock (CONTRIBUTING T7).
/// </summary>
/// <remarks>
/// A command is a plain delegate rather than a request flag on the state object: the view stays a
/// pure function of <see cref="SessionReadout"/>, and the window applies the change and saves the
/// configuration once per frame.
/// </remarks>
internal readonly record struct ViewCommands(
    Action Start,
    Action Stop,
    Action Reset,
    Action Lap,
    Action<bool> SetShowLaps,
    Action<HourFormat> SetHourFormat);
