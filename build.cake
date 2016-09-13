//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// VERSION
//////////////////////////////////////////////////////////////////////

var version = "3.5.0";
var displayVersion = "3.5.0";

//////////////////////////////////////////////////////////////////////
// NUGET PACKAGES
//////////////////////////////////////////////////////////////////////

var NUGET_PACKAGES = new []
{
  "NUnit.ConsoleRunner",
  "NUnit.Extension.VSProjectLoader",
  "NUnit.Extension.NUnitProjectLoader",
  "NUnit.Extension.NUnitV2Driver",
  "NUnit.Extension.NUnitV2ResultWriter",
  "NUnit.Extension.TeamCityEventListener"
};

//////////////////////////////////////////////////////////////////////
// FILE PATHS
//////////////////////////////////////////////////////////////////////

var ROOT_DIR = Context.Environment.WorkingDirectory.FullPath + "/";
var WIX_PROJ = ROOT_DIR + "nunit/nunit.wixproj";
var RESOURCES_DIR = ROOT_DIR + "resources/";
var PACKAGES_DIR = ROOT_DIR + "packages/";
var DISTRIBUTION_DIR = ROOT_DIR + "distribution/";
var IMAGE_DIR = ROOT_DIR + "image/";

//////////////////////////////////////////////////////////////////////
// TASK
//////////////////////////////////////////////////////////////////////

Task("FetchPackages")
.Does(() =>
{
    CleanDirectory(PACKAGES_DIR);

    var settings = new NuGetInstallSettings
    {
        OutputDirectory = PACKAGES_DIR
    };

    foreach(var package in NUGET_PACKAGES)
    {
        NuGetInstall(package, settings);
    }
});

Task("CreateImage")
.IsDependentOn("FetchPackages")
.Does(() =>
{
    CleanDirectory(IMAGE_DIR);
    CopyDirectory(RESOURCES_DIR, IMAGE_DIR);

    foreach(var directory in System.IO.Directory.GetDirectories(PACKAGES_DIR))
    {
        var lib = directory + "/lib";
        var tools = directory + "/tools";

        if (DirectoryExists(lib))
        CopyDirectory(lib, IMAGE_DIR);

        if (DirectoryExists(tools))
        CopyDirectory(tools, IMAGE_DIR);
    }
});

Task("PackageMsi")
.IsDependentOn("CreateImage")
.Does(() =>
{
    MSBuild(WIX_PROJ, new MSBuildSettings()
        .WithTarget("Rebuild")
        .SetConfiguration(configuration)
        .WithProperty("Version", version)
        .WithProperty("DisplayVersion", displayVersion)
        .WithProperty("OutDir", DISTRIBUTION_DIR)
        .WithProperty("Image", IMAGE_DIR)
        .SetMSBuildPlatform(MSBuildPlatform.x86)
        .SetNodeReuse(false)
        );
});


Task("Appveyor")
.IsDependentOn("PackageMsi");

Task("Default")
.IsDependentOn("PackageMsi");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
