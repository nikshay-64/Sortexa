namespace Sortexa.App.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string AppName => "Sortexa";
    public string Version => "1.0.0";
    public string ProductDescription => "A premium, safe, and beautifully designed Windows desktop file organizer. Sortexa enables effortless organization of your cluttered folders using smart categories, file extensions, creation and modification dates, size bands, semantic purpose, and custom rules—all with deterministic safety, pre-flight previews, and full Undo support.";

    public string DeveloperName => "Nikshay Kumar";
    public string DeveloperDepartment => "Electronics & Telecommunication Engineering";
    public string DeveloperInstitution => "Dayananda Sagar College of Engineering, Bengaluru";

    public string Tagline => "Your files. Organized beautifully.";
    public string MadeWithLoveAttribution => "Made with ❤️ - by Nikshay Kumar";
    public string LicenseInfo => "Sortexa Desktop Edition v1.0.0 © 2026 Nikshay Kumar. All rights reserved.";
}
