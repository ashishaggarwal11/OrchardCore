namespace OrchardCore.ReadingTime.Services;

/// <summary>
/// Provides services to calculate reading time estimates from HTML content.
/// </summary>
public interface IReadingTimeService
{
    /// <summary>
    /// Computes the estimated reading time in minutes from HTML content.
    /// </summary>
    /// <param name="html">The HTML content to analyze. Can be null or empty.</param>
    /// <returns>
    /// The estimated reading time in minutes, or null if the content contains no readable text.
    /// Calculation is based on a reading rate of 200 words per minute.
    /// </returns>
    int? ComputeReadingTimeMinutes(string html);
}
