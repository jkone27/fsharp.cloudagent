#if !FAKE
printfn "This build script is intended to be run with 'fake run build.fsx'."
printfn "Install fake-cli as a dotnet global tool: dotnet tool install -g fake-cli"
printfn "Run: fake run build.fsx [target]"
System.Environment.Exit(0)
#else

#r "nuget: Fake.Core.Target, 6.73.0"
#r "nuget: Fake.DotNet.Cli, 6.73.0"
#r "nuget: Fake.IO.FileSystem, 6.73.0"
#r "nuget: Fake.IO.Globbing, 6.73.0"
#r "nuget: Fake.DotNet.AssemblyInfoFile, 6.73.0"
#r "nuget: Fake.Core.ReleaseNotes, 6.73.0"

open System
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.DotNet.AssemblyInfoFile
open Fake.Core.ReleaseNotes

// Basic project configuration
let project = "FSharp.CloudAgent"
let solution = "FSharp.CloudAgent.sln"
let releaseNotesFile = "RELEASE_NOTES.md"

// Helpers
let configuration = Environment.environVarOrDefault "CONFIGURATION" "Release"

// Load release notes (optional)
let release =
    if System.IO.File.Exists releaseNotesFile then
        ReleaseNotes.load releaseNotesFile
    else
        { NugetVersion = "0.0.0"; AssemblyVersion = "0.0.0.0"; Notes = [] }

// Targets
Target.create "Clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"; "docs/output"]
)

Target.create "Restore" (fun _ ->
    DotNet.restore (fun opts -> { opts with Common = { opts.Common with WorkingDirectory = __SOURCE_DIRECTORY__ } }) solution
)

Target.create "AssemblyInfo" (fun _ ->
    let fsprojs = !! ("src/**/*.fsproj") // glob projects
    fsprojs
    |> Seq.iter (fun proj ->
        let projName = System.IO.Path.GetFileNameWithoutExtension(proj)
        let basePath = System.IO.Path.Combine("src", projName)
        let fileName = System.IO.Path.Combine(basePath, "AssemblyInfo.fs")
        CreateFSharpAssemblyInfo fileName
          [ AssemblyInfo.Title projName
            AssemblyInfo.Product project
            AssemblyInfo.Description "FSharp.CloudAgent"
            AssemblyInfo.Version release.AssemblyVersion
            AssemblyInfo.FileVersion release.AssemblyVersion ]
    )
)

Target.create "Build" (fun _ ->
    DotNet.build (fun opts -> { opts with Configuration = DotNet.BuildConfiguration.fromString configuration }) solution
)

Target.create "Test" (fun _ ->
    // Run all test projects using dotnet test
    !! "tests/**/*.fsproj"
    |> Seq.iter (fun proj -> DotNet.test (fun opts -> { opts with Configuration = DotNet.BuildConfiguration.fromString configuration }) proj)
)

Target.create "GenerateDocs" (fun _ ->
    // Requires fake-cli or dotnet-fsi availability for executing the F# script. This step is optional.
    let result = Shell.Exec("fake", "run docs/tools/generate.fsx GenerateDocs")
    if result <> 0 then failwith "Docs generation failed"
)

Target.create "All" ignore

// Define dependencies
open Fake.Core.TargetOperators

"Clean"
  ==> "Restore"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Test"
  ==> "All"

// Default target
Target.runOrDefault "All"

#endif
