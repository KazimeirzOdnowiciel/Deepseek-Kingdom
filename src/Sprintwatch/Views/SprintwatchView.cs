using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Glasswork;
using Glasswork.Backend;
using Sprintwatch.Services;

namespace Sprintwatch.Views;

/// <summary>
/// Draws the main window's body from a <see cref="SessionReadout"/> and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// No Dalamud service is touched here, no configuration, and no wall clock: the window formats the
/// session into a readout first, and the headless preview draws this same view with fixed sample
/// data (CONTRIBUTING T6, T7). Time is never read here; every duration arrives already formatted.
/// </para>
/// <para>
/// The body sits in a <c>Gw.BeginCard()</c> panel rather than straight on the window, which is what
/// makes it readable both over the glass in game and over the preview's backdrop stand-in. Colour
/// is never painted by hand: every widget takes its ink from the Look (N9).
/// </para>
/// </remarks>
internal static class SprintwatchView
{
    private const int TwentyFourHourIndex = 0;
    private const int TwelveHourIndex = 1;

    /// <summary>
    /// Labels for <c>Gw.Segmented</c>: index 0 is the 24-hour clock. A static array, because the
    /// widget re-reads the labels every frame and the draw path must not allocate.
    /// </summary>
    private static readonly string[] HourFormats = { "24-hour", "12-hour" };

    /// <summary>
    /// Draws the whole body. <paramref name="readout"/> is the frame's state and
    /// <paramref name="commands"/> is the only way anything here can affect the plugin.
    /// </summary>
    internal static void Draw(in SessionReadout readout, in ViewCommands commands)
    {
        if (Gw.BeginCard())
        {
            Gw.Heading("Sprint Watch"u8);

            DrawSession(readout);
            DrawControls(readout, commands);
            DrawLaps(readout, commands);
        }

        Gw.EndCard();
    }

    private static void DrawSession(in SessionReadout readout)
    {
        if (Gw.BeginSection("Session"u8))
        {
            Gw.BeginRow("Elapsed"u8);
            Gw.Text(readout.ElapsedText, TextRole.LargeTitle);
            Gw.EndRow();

            Gw.BeginRow("Lap total"u8, "The cumulative time at the last lap."u8);
            Gw.Text(readout.TotalText, TextRole.Title2);
            Gw.EndRow();
        }

        Gw.EndSection();
    }

    private static void DrawControls(in SessionReadout readout, in ViewCommands commands)
    {
        if (Gw.BeginSection("Controls"u8))
        {
            DrawAction(commands.Start, readout.CanStart, "Start"u8, ButtonStyle.Prominent);
            ImGui.SameLine();

            DrawAction(commands.Stop, readout.CanStop, "Stop"u8, ButtonStyle.Default);
            ImGui.SameLine();

            DrawAction(commands.Lap, readout.CanLap, "Lap"u8, ButtonStyle.Tinted);
            ImGui.SameLine();

            DrawAction(commands.Reset, readout.CanReset, "Reset"u8, ButtonStyle.Destructive);
        }

        Gw.EndSection();
    }

    private static void DrawAction(Action action, bool enabled, ImU8String label, ButtonStyle style)
    {
        ImGui.BeginDisabled(!enabled);

        if (Gw.Button(label, style))
        {
            action();
        }

        ImGui.EndDisabled();
    }

    private static void DrawLaps(in SessionReadout readout, in ViewCommands commands)
    {
        if (Gw.BeginSection("Laps"u8, "The oldest lap drops off once the table is full."u8))
        {
            Gw.BeginRow("Show laps"u8, "Hiding the table keeps the session and its laps."u8);

            var showLaps = readout.ShowLaps;

            if (Gw.Toggle(ref showLaps, "##showLaps"u8))
            {
                commands.SetShowLaps(showLaps);
            }

            Gw.EndRow();

            if (readout.ShowLaps)
            {
                DrawLapBody(readout, commands);
            }
        }

        Gw.EndSection();
    }

    /// <summary>
    /// The 24-hour/12-hour selector for the table's clock column.
    /// </summary>
    /// <remarks>
    /// The readout does not carry the current <see cref="HourFormat"/> - the Services contract is
    /// fixed and only the formatted lap clocks depend on it - so the selected segment is inferred
    /// from those clocks: a lap clock that ends in <c>AM</c> or <c>PM</c> is the 12-hour form.
    /// Drawn only when the table has a row, so the inference is exact and the control never shows a
    /// state the lap times contradict.
    /// </remarks>
    private static void DrawLapClock(in SessionReadout readout, in ViewCommands commands)
    {
        Gw.BeginRow("Lap clock"u8, "How the time of day reads in the table."u8);

        var format = ClockFormatIndex(readout);

        if (Gw.Segmented(ref format, HourFormats))
        {
            commands.SetHourFormat(format == TwelveHourIndex ? HourFormat.TwelveHour : HourFormat.TwentyFourHour);
        }

        Gw.EndRow();
    }

    private static void DrawLapBody(in SessionReadout readout, in ViewCommands commands)
    {
        if (readout.Laps.Count == 0)
        {
            Gw.Text("No laps recorded yet."u8, TextRole.Footnote);
            return;
        }

        DrawLapClock(readout, commands);
        DrawLapTable(readout);
    }

    private static void DrawLapTable(in SessionReadout readout)
    {
        if (!Gw.BeginTable("##laps"u8, 4, GwTableFlags.Zebra | GwTableFlags.Borderless, Vector2.Zero))
        {
            return;
        }

        Gw.TableColumn("Lap"u8, 64f, false);
        Gw.TableColumn("Split"u8, 1f, true, false, true);
        Gw.TableColumn("Total"u8, 1f, true, false, true);
        Gw.TableColumn("Clock"u8, 1f, true, false, true);
        Gw.TableHeaders();

        foreach (var lap in readout.Laps)
        {
            // A lap's number is the readout's stable identity for it, so a row keeps its id while
            // later laps are appended or the oldest is dropped and the survivors renumbered.
            Gw.TableRow(lap.Number);

            Gw.TableCell();
            Gw.TableText(lap.NumberText, TextRole.Body);

            Gw.TableCell();
            Gw.TableText(lap.SplitText, TextRole.Mono);

            Gw.TableCell();
            Gw.TableText(lap.TotalText, TextRole.Mono);

            Gw.TableCell();
            Gw.TableText(lap.ClockText, TextRole.Mono);
        }

        Gw.EndTable();
    }

    private static int ClockFormatIndex(in SessionReadout readout)
    {
        foreach (var lap in readout.Laps)
        {
            if (lap.ClockText.EndsWith("AM", StringComparison.Ordinal)
                || lap.ClockText.EndsWith("PM", StringComparison.Ordinal))
            {
                return TwelveHourIndex;
            }
        }

        return TwentyFourHourIndex;
    }
}
