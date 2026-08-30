using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Reading Time",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Description = "The Reading Time module enables content items with HtmlBodyPart to display an estimated reading time badge.",
    Dependencies = ["OrchardCore.Html"],
    Category = "Content Management"
)]
