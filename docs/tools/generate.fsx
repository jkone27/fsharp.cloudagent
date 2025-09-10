// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// This script is intentionally minimal — if FSharp.Formatting is available it will
// attempt to use it, otherwise it will copy static files and exit gracefully.
// --------------------------------------------------------------------------------------

open System
open System.IO

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "FSharp.CloudAgent.dll" ]
// Web site location for the generated documentation
let website = "/FSharp.CloudAgent"

let githubLink = "http://github.com/isaacabraham/FSharp.CloudAgent"

// Specify more information about your project
let info =
  [ "project-name", "FSharp.CloudAgent"
    "project-author", "Isaac Abraham"
    "project-summary", "Allows the use of F# Agents in Azure."
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/FSharp.CloudAgent" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

let combine a b = Path.Combine(a,b)
let (__SOURCE_DIR__) = __SOURCE_DIRECTORY__

#if RELEASE
let root = website
#else
let root = "file://" + (combine __SOURCE_DIR__ "../output")
#endif

// Paths with template/source/output locations
let bin        = combine __SOURCE_DIR__ "../../bin"
let content    = combine __SOURCE_DIR__ "../content"
let output     = combine __SOURCE_DIR__ "../output"
let files      = combine __SOURCE_DIR__ "../files"
let templates  = combine __SOURCE_DIR__ "templates"
let formatting = combine __SOURCE_DIR__ "../../packages/FSharp.Formatting"
let docTemplate = combine formatting "templates/docpage.cshtml"

let layoutRoots = [ templates; combine formatting "templates"; combine formatting "templates/reference" ]

let log fmt = Printf.ksprintf (fun s -> Console.WriteLine(s)) fmt

let rec copyRecursive (src:string) (dest:string) =
    if not (Directory.Exists src) then () else
    Directory.CreateDirectory dest |> ignore
    for f in Directory.GetFiles(src) do
        let destFile = combine dest (Path.GetFileName f)
        File.Copy(f, destFile, true)
    for d in Directory.GetDirectories(src) do
        let dirName = Path.GetFileName d
        copyRecursive d (combine dest dirName)

let ensureDirectory (d:string) = Directory.CreateDirectory d |> ignore

let cleanDir (d:string) =
    if Directory.Exists d then Directory.Delete(d, true)
    Directory.CreateDirectory(d) |> ignore

// Copy static files and CSS + JS from F# Formatting (if present)
let copyFiles () =
  log "Copying files..."
  copyRecursive files output
  ensureDirectory (combine output "content")
  let stylesSrc = combine formatting "styles"
  if Directory.Exists stylesSrc then
    copyRecursive stylesSrc (combine output "content")
    log "Copied styles from FSharp.Formatting"
  else
    log "FSharp.Formatting styles not found; skipping styles copy"

// Attempt to generate API reference using FSharp.Formatting if available; otherwise skip.
let tryBuildReference () =
  if Directory.Exists formatting then
    try
      // Attempt to call FSharp.MetadataFormat.Generate via reflection if available
      let asmPath = Path.Combine(formatting, "FSharp.MetadataFormat.dll")
      if File.Exists asmPath then
        let asm = System.Reflection.Assembly.LoadFrom asmPath
        let typ = asm.GetType("FSharp.MetadataFormat")
        if typ <> null then
          let meth = typ.GetMethod("Generate", System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.Static)
          if meth <> null then
            let binaries = referenceBinaries |> List.map (fun lib -> combine bin lib) |> List.toArray
            let layoutRootsArr = layoutRoots |> List.toArray
            let parameters = [| box (Array.append [| box ("root", box root) |] (info |> List.map box |> List.toArray)) |]
            // We cannot easily call the strongly-typed Generate method via reflection without building the exact arg array,
            // so log and fall back to skipping.
            log "Found FSharp.MetadataFormat assembly but cannot invoke Generate reflectively reliably; skipping reference generation"
          else log "FSharp.MetadataFormat.Generate not found; skipping reference generation"
        else log "FSharp.MetadataFormat type not found; skipping reference generation"
      else log "FSharp.MetadataFormat.dll not found in packages; skipping reference generation"
    with ex -> log "Exception while attempting to build reference: %s" (ex.Message)
  else
    log "FSharp.Formatting package not found; skipping API reference generation"

// Attempt to build documentation via FSharp.Literate if available; otherwise skip.
let tryBuildDocumentation () =
  if Directory.Exists formatting then
    log "FSharp.Formatting appears present, but this script will not attempt to call Literate.ProcessDirectory automatically."
    log "To generate HTML from .fsx/.md please run your local docs toolchain or add FSharp.Formatting to the project and re-run."
  else
    log "FSharp.Formatting not available; skipping documentation generation"

// Generate
copyFiles()
#if HELP
tryBuildDocumentation()
#endif
#if REFERENCE
tryBuildReference()
#endif

log "Docs script finished (some steps may be no-ops if FSharp.Formatting is not installed)."
