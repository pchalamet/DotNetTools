
open System.IO
open System.Linq
open System.Xml.Linq

#nowarn "0077" // op_Explicit
let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))


let NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")
let NsNone = XNamespace.None



let convertPackageConfig (projectFile : FileInfo) =
    let packageFileName = projectFile.Directory.GetFiles("packages.config").SingleOrDefault()
    if packageFileName |> isNull |> not then
        let xdoc = XDocument.Load (packageFileName.FullName)
        packageFileName.Delete()

        let pkgs = xdoc.Descendants(NsNone + "package")
                     |> Seq.map (fun x -> XElement(NsNone + "PackageReference",
                                             XAttribute(NsNone + "Include", (!> x.Attribute(NsNone + "id") : string)),
                                             XAttribute(NsNone + "Version", (!> x.Attribute(NsNone + "version") : string))))
        XElement(NsNone + "ItemGroup", pkgs)
    else 
        null        



// http://www.natemcmaster.com/blog/2017/03/09/vs2015-to-vs2017-upgrade/
let convertProject (projectFile : FileInfo) =
    let projectFileName = projectFile.Name |> Path.GetFileNameWithoutExtension
    let xdoc = XDocument.Load (projectFile.FullName)

    let rewrite (xel : XElement) =
        if xel |> isNull |> not then
            XElement(NsNone + xel.Name.LocalName,
                xel.Attributes() |> Seq.map (fun x -> XAttribute(NsNone + x.Name.LocalName, x.Value)),
                xel.Nodes() |> Seq.filter (fun x -> x.NodeType = System.Xml.XmlNodeType.Text))
        else 
            null

    // https://docs.microsoft.com/en-us/dotnet/standard/frameworks
    let xTargetFx = match !> xdoc.Descendants(NsMsBuild + "TargetFrameworkVersion").First() : string with
                    | "v3.5" -> "net35"
                    | "v4.0" -> "net40"
                    | "v4.5" -> "net45"
                    | "v4.5.1" -> "net451"
                    | "v4.5.2" -> "net452"
                    | "v4.6" -> "net46"
                    | "v4.6.1" -> "net461"
                    | "v4.6.2" -> "net462"
                    | "v4.7" -> "net47"
                    | "v4.7.1" -> "net471"
                    | "v4.7.2" -> "net472"
                    | x -> failwithf "Unsupported TargetFrameworkVersion %A" x
                    |> (fun x -> XElement(NsNone + "TargetFramework", x))

    let xPlatform = match !> xdoc.Descendants(NsMsBuild + "Platform").FirstOrDefault() : string with
                    | null -> null
                    | "AnyCPU" -> null
                    | x -> XElement(NsNone + "PlatformTarget", x)

    let xunsafe = xdoc.Descendants(NsMsBuild + "AllowUnsafeBlocks").FirstOrDefault() |> rewrite

    let xSignAss = xdoc.Descendants(NsMsBuild + "SignAssembly").FirstOrDefault() |> rewrite

    let xOriginatorSign = xdoc.Descendants(NsMsBuild + "AssemblyOriginatorKeyFile").FirstOrDefault() |> rewrite

    let xAssName = match !> xdoc.Descendants(NsMsBuild + "AssemblyName").FirstOrDefault() : string with
                   | x when x <> projectFileName -> XElement(NsNone + "AssemblyName", x)
                   | _ -> null

    let xRootNs = match !> xdoc.Descendants(NsMsBuild + "RootNamespace").FirstOrDefault() : string with
                  | x when x <> projectFileName -> XElement(NsNone + "RootNamespace", x)
                  | _ -> null

    let xOutType = match !> xdoc.Descendants(NsMsBuild + "OutputType").FirstOrDefault() : string with
                   | null -> null
                   | "Library" -> null
                   | x -> XElement(NsNone + "OutputType", x)

    let xWarnAsErr = xdoc.Descendants(NsMsBuild + "TreatWarningsAsErrors").FirstOrDefault() |> rewrite

    let xWarns = xdoc.Descendants(NsMsBuild + "WarningsAsErrors").FirstOrDefault() |> rewrite

    let isFSharp = xdoc.Descendants(NsMsBuild + "Compile")
                    |> Seq.exists (fun x -> (!> x.Attribute(NsNone + "Include") : string).EndsWith(".fs"))
    let src = xdoc.Descendants(NsMsBuild + "Compile")
                    |> Seq.filter (fun x -> isFSharp || (!> x.Attribute(NsNone + "Include") : string).StartsWith(".."))
                    |> Seq.map rewrite
    let res = xdoc.Descendants(NsMsBuild + "EmbeddedResource")
                    |> Seq.filter (fun x -> (!> x.Attribute(NsNone + "Include") : string).StartsWith(".."))
                    |> Seq.map rewrite
    let content = xdoc.Descendants(NsMsBuild + "Content")
                    |> Seq.map rewrite
    let none = xdoc.Descendants(NsMsBuild + "None")
                    |> Seq.filter (fun x -> (!> x.Attribute(NsNone + "Include") : string) <> "packages.config")
                    |> Seq.map rewrite
    let xSrc = XElement(NsNone + "ItemGroup", src, content, res, none)

    let prjRefs = xdoc.Descendants(NsMsBuild + "ProjectReference")
                    |> Seq.map rewrite
    let xPrjRefs = if prjRefs.Any() then XElement(NsNone + "ItemGroup", prjRefs)
                   else null

    let refs = xdoc.Descendants(NsMsBuild + "Reference")
                    |> Seq.filter (fun x -> x.HasElements |> not)
                    |> Seq.map rewrite
    let xRefs = if refs.Any() then XElement(NsNone + "ItemGroup", refs)
                else null

    let xPkgs = convertPackageConfig projectFile

    XDocument(
        XElement(NsNone + "Project",
            XAttribute(NsNone + "Sdk", "Microsoft.NET.Sdk"),
            XElement(NsNone + "PropertyGroup",
                XElement(NsNone + "AutoGenerateBindingRedirects", "true"),
                XElement(NsNone + "GenerateAssemblyInfo", "false"),
                xTargetFx, 
                xPlatform, 
                xunsafe,
                xSignAss,
                xOriginatorSign,
                xAssName,
                xRootNs,
                xOutType,
                xWarnAsErr,
                xWarns),
            xRefs,
            xPkgs,
            xSrc,
            xPrjRefs))

let isCandidate (projectFile : FileInfo) =
    let xdoc = XDocument.Load (projectFile.FullName)
    xdoc.Root.Attribute(NsNone + "Sdk") |> isNull


let saveProject (projectFile : FileInfo, content : XDocument) =
    content.Save (projectFile.FullName)


let searchProjects (rootFolder : DirectoryInfo) =
    rootFolder.EnumerateFiles("*.csproj", SearchOption.AllDirectories)
        |> Seq.append (rootFolder.EnumerateFiles("*.vbproj", SearchOption.AllDirectories))
        |> Seq.append (rootFolder.EnumerateFiles("*.fsproj", SearchOption.AllDirectories))

[<EntryPoint>]
let main argv = 
    if argv |> Array.length = 0 then failwithf "Usage: ConvertToMsBuildSdk <folder>"
    let rootFolder = argv.[0] |> DirectoryInfo
    if rootFolder.Exists |> not then failwithf "Directory %A does not exist" rootFolder

    rootFolder |> searchProjects 
               |> Seq.filter isCandidate
               |> Seq.map (fun x -> x, convertProject x)
               |> Seq.iter saveProject
    0

   