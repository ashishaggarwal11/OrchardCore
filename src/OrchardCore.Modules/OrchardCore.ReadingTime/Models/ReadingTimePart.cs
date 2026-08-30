using OrchardCore.ContentManagement;

namespace OrchardCore.ReadingTime.Models;

public sealed class ReadingTimePart : ContentPart
{
    public int? ReadingTimeMinutes { get; set; }
}
