#if BOOT
open Fake
module FB = Fake.Boot
FB.Prepare {
    FB.Config.Default __SOURCE_DIRECTORY__ with
        NuGetDependencies =
            let ( ! ) x = FB.NuGetDependency.Create x
            [
                 !"IntelliFactory.Build"
                 !"FSUnit"
                 !"NUnit"
                 !"NUnit.Runners"
            ]
}
#else
#load ".build/boot.fsx"

open System
open System.IO
open Fake
module B = IntelliFactory.Build.CommonBuildSetup
module F = IntelliFactory.Build.FileSystem
module NG = IntelliFactory.Build.NuGetUtils
module X = IntelliFactory.Build.XmlGenerator

let (+/) a b = Path.Combine(a, b)
let rootDir = __SOURCE_DIRECTORY__
let packagesDir = rootDir+/"packages"
let toolsDir = packagesDir+/"tools"
let T x f = Target x f; x

module Config =
    let authors = "Harry Pierson and Ryan Riley"
    let owners = "panesofglass"
    let packageId = "Cashel"
    let description = "A simple monadic parser combinator library."
    let licenseUrl = "https://github.com/DevHawk/cashel/blob/master/LICENSE.txt"
    let assemblyVersion = Version "1.0.0"
    let assemblyFileVersion = Version "1.0.0"
    let version = "1.0.0"
    let mail = "ryan.riley@panesofglass.org"
    let homepage = "https://github.com/devhawk/cashel"
    let tags = ["F#";"parser";"PEG"]
    let website = "https://github.com/DevHawk/cashel"

let metadata =
    B.Metadata.Create(
        Author = Some Config.authors,
        AssemblyVersion = Some Config.assemblyVersion,
        FileVersion = Some Config.assemblyFileVersion,
        Description = Some Config.description,
        Product = Some Config.packageId,
        Website = Some Config.website)

let mainFrameworks = [B.Net20; B.Net40; B.Net45]

let mainConfigs : list<B.BuildConfiguration> =
    [
        for fw in mainFrameworks ->
            {
                ConfigurationName = "Release"
                Debug = false
                FrameworkVersion = fw
                NuGetDependencies = new NuGet.PackageDependencySet(fw.ToFrameworkName(), [])
            }
    ]

let testFrameworks = [B.Net45]

let testConfigs : list<B.BuildConfiguration> =
    [
        for fw in testFrameworks ->
            {
                ConfigurationName = "Release"
                Debug = false
                FrameworkVersion = fw
                NuGetDependencies =
                    let (!) x = new NuGet.PackageDependency(x)
                    let deps =
                        [
                            !"FsUnit"
                            !"NUnit"
                        ]
                    new NuGet.PackageDependencySet(fw.ToFrameworkName(), deps)
            }
    ]

let defProject configs (name: string) : B.Project =
    {
        Name = name
        BuildConfigurations = configs
        MSBuildProjectFilePath = Some (rootDir +/ name +/ (name + ".fsproj"))
    }

let solution : B.Solution =
    {
        Metadata = metadata
        Projects =
            [
                defProject mainConfigs "Cashel"
                defProject testConfigs "Cashel.Tests"
                defProject testConfigs "Cashel.Sample.Peg"
            ]
        RootDirectory = rootDir
    }

let buildMain =
    T "BuildMain" (fun () ->
        solution.MSBuild()
        |> Async.RunSynchronously)

let cleanMain =
    T "CleanMain" (fun () ->
        solution.MSBuild {
            BuildConfiguration = None
            Properties = Map []
            Targets = ["Clean"]
        }
        |> Async.RunSynchronously)

let build = T "Build" ignore
let clean = T "Clean" ignore

let dotBuildDir = rootDir+/".build/"

let buildNuGetPackage = T "BuildNuGetPackage" <| fun () ->
    let version = new NuGet.SemanticVersion(Config.assemblyFileVersion)
    let content =
        use out = new MemoryStream()
        let builder = new NuGet.PackageBuilder()
        builder.Id <- Config.packageId
        builder.Version <- version
        builder.Authors.Add(Config.authors) |> ignore
        builder.Owners.Add(Config.authors) |> ignore
        builder.LicenseUrl <- Uri(Config.licenseUrl)
        builder.ProjectUrl <- Uri(Config.website)
        builder.Copyright <- String.Format("Copyright (c) {0} {1}", DateTime.Now.Year, Config.authors)
        builder.Description <- Config.description
        Config.tags
        |> Seq.iter (builder.Tags.Add >> ignore)
        for cfg in mainConfigs do
            for ext in [".xml"; ".dll"] do
                let n = Config.packageId
                let f = new NuGet.PhysicalPackageFile()
                let conf =
                    let fw = cfg.FrameworkVersion.GetMSBuildLiteral()
                    String.Format("{0}-{1}", cfg.ConfigurationName, fw)
                let netXX = cfg.FrameworkVersion.GetNuGetLiteral()
                f.SourcePath <- rootDir+/n+/"bin"+/conf+/(n + ext)
                f.TargetPath <- "lib"+/netXX+/(n + ext)
                builder.Files.Add(f)
        builder.Save(out)
        F.Binary.FromBytes (out.ToArray())
        |> F.BinaryContent
    let out = rootDir+/".build"+/String.Format("{0}.{1}.nupkg", Config.packageId, version)
    content.WriteFile(out)
    tracefn "Written %s" out

let runTests = T "test" <| fun () ->
    let runner =
        !! @"packages\NUnit.Runners.*\tools\nunit-console.exe"
        |> Seq.head
    !! @"Cashel.Tests\bin\**\Cashel.Tests.dll"
    |> NUnit (fun cfg ->
        { cfg with
            ToolPath = Path.GetDirectoryName(runner)
            Framework = "net-4.5"
        })

let boilerplate = T "boilerplate" <| fun () ->
    B.Prepare (tracefn "%s") rootDir

buildMain
    ==> buildNuGetPackage
    ==> build

cleanMain
    ==> clean

RunTargetOrDefault build

#endif
