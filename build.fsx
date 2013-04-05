#r "packages/FAKE.1.74.139.0/tools/FakeLib.dll"
#r "packages/IntelliFactory.Build.0.0.6/lib/net40/IntelliFactory.Build.dll"
#r "packages/DotNetZip.1.9.1.8/lib/net20/Ionic.Zip.dll"

open System
open System.IO
open System.Net
open Fake
open Ionic.Zip
module B = IntelliFactory.Build.CommonBuildSetup
module NG = IntelliFactory.Build.NuGet
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

let frameworks = [B.Net20; B.Net40; B.Net45]

let MainSolution =
    B.Solution.Standard rootDir metadata [
        B.Project.FSharp "Cashel" frameworks
    ]
let buildMain = T "BuildMain" MainSolution.Build
let cleanMain = T "CleanMain" MainSolution.Clean

let TestSolution =
    B.Solution.Standard rootDir metadata [
        B.Project.FSharp "Cashel" frameworks
        B.Project.FSharp "Cashel.Tests" frameworks
    ]
let buildTest = T "BuildTest" TestSolution.Build
let cleanTest = T "CleanTest" TestSolution.Clean

let PegSolution =
    B.Solution.Standard rootDir metadata [
        B.Project.FSharp "Cashel" frameworks
        B.Project.FSharp "Cashel.Sample.Peg" frameworks
    ]
let buildPeg = T "BuildPeg" PegSolution.Build
let cleanPeg = T "CleanPeg" PegSolution.Clean

let prepareTools =
    T "PrepareTools" <| fun () ->
        Fake.FileSystemHelper.ensureDirectory toolsDir

let cleanTools =
    T "CleanTools" <| fun () ->
        Directory.Delete(toolsDir, true)

let build = T "Build" ignore
let clean = T "Clean" ignore

let dotBuildDir = rootDir+/".build/"

let buildNuSpecXml () =
    let e n = X.Element.Create n
    let ( -- ) (a: X.Element) (b: string) = X.Element.WithText b a
    e "package" - [
        e "metadata" - [
            e "id" -- Config.packageId
            e "version" -- Config.version
            e "authors"-- Config.authors
            e "owners"-- Config.owners
            e "language"-- "en-US"
            e "licenseUrl" -- Config.licenseUrl
            e "projectUrl"-- Config.website
            e "requireLicenseAcceptance" -- "false"
            e "description" -- Config.description
            e "copyright" -- sprintf "Copyright (c) %O %s" DateTime.Now.Year Config.authors
            e "tags" -- String.concat " " Config.tags
        ]
        e "files" - [
            e "file" + ["src", @"root\net20\*.*"; "target", @"tools\net20"]
            e "file" + ["src", @"root\net40\*.*"; "target", @"tools\net40"]
            e "file" + ["src", @"root\net45\*.*"; "target", @"tools\net45"]
        ]
    ]

let nugetPackageFile =
    dotBuildDir +/ sprintf "%s.%s.nupkg" Config.packageId Config.version

let buildNuGet =
    T "BuildNuGet" <| fun () ->
        ensureDirectory dotBuildDir
        let nuspec = dotBuildDir+/"Cashel.nuspec"
        X.WriteFile nuspec (buildNuSpecXml())
        for f in frameworks do
            let rDir = dotBuildDir+/"root"+/f.GetNuGetLiteral()
            if Directory.Exists rDir then
                Directory.Delete(rDir, true)
            ensureDirectory rDir
            let config = "Release-" + f.GetMSBuildLiteral()
            let prefix = rootDir +/ "*" +/ "bin" +/ config
            (!+ (prefix +/ "*.dll")
                ++ (prefix +/ "*.xml")
                ++ (prefix +/ "*.exe")
                ++ (prefix +/ "*.exe.config"))
            |> Scan
            |> Seq.filter (fun x ->
                [
                    "mscorlib.dll"
                    "system.dll"
                    "system.core.dll"
                    "system.numerics.dll"
                    "tests.dll"
                    "tests.xml"
                    "tests.exe"
                ]
                |> List.forall (fun n -> not (x.ToLower().EndsWith n)))
            |> Seq.distinct
            |> Copy rDir
            do !! (rootDir +/ "build" +/ "DeployedTargets" +/ "*.targets") |> Copy rDir
        let nugetExe = rootDir+/".nuget"+/"NuGet.exe"
        nuspec
        |> NuGetPack (fun p ->
            { p with
                OutputPath = dotBuildDir
                ToolPath   = nugetExe
                Version    = Config.version
                WorkingDir = dotBuildDir
            })

let zipPackageFile =
    rootDir +/ "Web" +/ "downloads" +/ sprintf "%s-%s.zip" Config.packageId Config.version

let buildZipPackage =
    T "BuildZipPackage" <| fun () ->
        ensureDirectory (Path.GetDirectoryName zipPackageFile)
        let zip = new ZipFile()
        let addFile path =
            zip.AddEntry(Path.GetFileName path, File.ReadAllBytes path) |> ignore
        addFile nugetPackageFile
        addFile (rootDir+/"LICENSE.txt")
        zip.Save zipPackageFile

prepareTools
    ==> buildMain
    ==> buildTest // Need to also runTests
    //==> runTests
    ==> buildPeg
    ==> buildNuGet
    ==> buildZipPackage
    ==> build

prepareTools
    ==> cleanMain
    ==> cleanTest
    ==> cleanPeg
    ==> cleanTools
    ==> clean

RunTargetOrDefault "Build"

(*
Target? Test <-
    fun _ ->
        !+ (testDir + "/*.dll")
          |> Scan
          |> NUnit (fun p -> 
                      {p with 
                         ToolPath = nunitPath; 
                         DisableShadowCopy = true; 
                         OutputFile = nunitOutput}) 

Target? GenerateDocumentation <-
    fun _ ->
      !+ (buildDir + "Cashel.dll")      
        |> Scan
        |> Docu (fun p ->
            {p with
               ToolPath = "./lib/FAKE/docu.exe"
               TemplatesPath = "./lib/FAKE/templates"
               OutputPath = docsDir })

Target? CopyLicense <-
    fun _ ->
        [ "LICENSE.txt" ] |> CopyTo buildDir

Target? BuildZip <-
    fun _ -> Zip buildDir zipFileName filesToZip

Target? ZipDocumentation <-
    fun _ ->    
        let docFiles = 
          !+ (docsDir + "/**/*.*")
            |> Scan
        let zipFileName = deployDir + sprintf "Documentation-%s.zip" version
        Zip docsDir zipFileName docFiles

Target? Default <- DoNothing
Target? Deploy <- DoNothing

// Dependencies
For? BuildApp <- Dependency? Clean
For? Test <- Dependency? BuildApp |> And? BuildTest
For? GenerateDocumentation <- Dependency? BuildApp
For? ZipDocumentation <- Dependency? GenerateDocumentation
For? BuildZip <- Dependency? BuildApp |> And? CopyLicense
For? CreateNuGet <- Dependency? Test |> And? BuildZip |> And? ZipDocumentation
For? Deploy <- Dependency? Test |> And? BuildZip |> And? ZipDocumentation
For? Default <- Dependency? Deploy

*)
