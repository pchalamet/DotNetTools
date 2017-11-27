# ConvertToMsBuildSdk
ConvertToMsBuildSdk converts msbuild .NET projects to new project style (Sdk).

Usage:
```
  ConvertToMsBuildSdk <folder>
```

It will look for projects (*.csproj, *.vbproj, *.fsproj) and convert them to new format.

What is being converted is covered here for the most part:
http://www.natemcmaster.com/blog/2017/03/09/vs2015-to-vs2017-upgrade/

## Restrictions
* NuGet packages are converted to PackageReference. Paket is not supported for the moment.
* Imports are discarded
* DefineConstants are discarded
* AutoGenerateBindingRedirects is forced to true
* GenerateAssemblyInfo is forced to false
* only .net fx 3.5+ is supported

## Disclaimer
Converted projects are overwritten !!
I can't be liable if you lose something or things do not work as expected !
You have been warned !

## License
Do what you want with this source code!
