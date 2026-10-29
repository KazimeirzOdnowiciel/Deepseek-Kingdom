namespace Sprintwatch.Services;

/// <summary>
/// A lap already formatted for display. Every string is produced here in the invariant culture, so
/// the view only lays text out and never formats a number or a time itself (CONTRIBUTING T7).
/// </summary>
/// <remarks>
/// <see cref="Number"/> is the stable identity a view uses for per-row ImGui ids;
/// <see cref="NumberText"/> is what the row shows.
/// </remarks>
internal sealed record LapRow(
    int Number,
    string NumberText,
    string SplitText,
    string TotalText,
    string ClockText);
