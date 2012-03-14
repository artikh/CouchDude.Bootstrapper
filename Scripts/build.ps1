properties {
    Import-Module .\teamcity.psm1

    $config = 'Debug'
    $isDebug = $conifg -eq 'Debug'
    
    $scriptDir = (resolve-path .).Path
    $rootDir = (resolve-path ..).Path;
    $buildDir = "$rootDir\Build";   
    $srcDir = "$rootDir\Src";
    $assemblyInfoFileName = "$srcDir\GlobalAssemblyInfo.cs"

    if ($version -eq $null) {
        $globalAssemblyInfo = (cat $assemblyInfoFileName)

        $match = [regex]::Match($globalAssemblyInfo, '\[assembly: AssemblyVersion\("([\.\d]+)"\)\]')
        $version = $match.Groups[1].Value
    }
    
    if ($nugetSources -eq $null) {
        $nugetSources = "https://go.microsoft.com/fwlink/?LinkID=206669;"
    }
}

task default -depends setVersion, package

task clean {
    if(test-path $buildDir) {
        dir $buildDir | del -Recurse -Force
    }
    mkdir -Force $buildDir > $null
}

task setVersion {  
    $assembyInfo = [System.IO.File]::ReadAllText($assemblyInfoFileName)
    $assembyInfo = $assembyInfo -replace "Version\((.*)\)]", "Version(`"$version`")]"
    $assembyInfo.Trim() > $assemblyInfoFileName
}


task installPackages {
    dir -Recurse -Filter packages.config | %{    
        exec { ..\tools\nuget\NuGet.exe install $_ -Source $nugetSources -OutputDirectory "$srcDir\Packages"  }
    }
}

task build -depends clean, installPackages {
    exec { msbuild "$srcDir\Bootstrapper.sln" /nologo /p:Config=$config /p:Platform='Any Cpu' /maxcpucount /verbosity:minimal }    
}

function replaceToken([string]$fileName, [string]$tokenName, [string]$tokenValue) {
    $content = (cat $fileName)
    $content | % { $_.Replace($tokenName, $tokenValue) } > $fileName
}

$nuSpecNamespace = 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'

function prepareNuspec([string]$templateFileName, [string]$targetFileName, [array]$fileTemplates) {
    $specXml = [xml](get-content $templateFileName)
    
    $filesNode = $specXml.CreateElement('files', $nuSpecNamespace);   
    foreach($fileTemplate in $fileTemplates) {
        foreach($fileName in (resolve-path $fileTemplate | %{ $_.Path } )) {
            $fileNode = $specXml.CreateElement('file', $nuSpecNamespace);
            $fileNode.SetAttribute('src', $fileName);
            $fileNode.SetAttribute('target', 'lib\net40');
            $filesNode.AppendChild($fileNode);
        }
    }
    $specXml.package.AppendChild($filesNode);

    $specXml.Save($targetFileName);
}

function createOrClear($dirName) {
    if((test-path $dirName)) {
        rm -Recurse -Force $dirName 
    }
    mkdir -Force $dirName
    return $dirName
}

function packNuGet([string]$nuspecFile) {
    exec { ..\tools\nuget\NuGet.exe pack $nuspecFile -OutputDirectory $buildDir }    
}

function prepareAndPackage([string]$templateNuSpec, [array]$fileTemplates) {
    $nuspecName = [System.IO.Path]::GetFileNameWithoutExtension($templateNuSpec)
    $nuspecFile = "$buildDir\$nuspecName.nuspec"

    prepareNuspec -template "$templateNuspec" -target $nuspecFile -fileTemplates $fileTemplates > $null
    replaceToken -file $nuspecFile -tokenName '$version$' -tokenValue $version

    packNuGet $nuspecFile

    del $nuspecFile
}

task package -depends build {
    prepareAndPackage -templateNuSpec "$srcDir\Core\CouchDude.Bootstrapper.nuspec" -fileTemplates ("$srcDir\Core\bin\$config\CouchDude.Bootstrapper.*")
    TeamCity-PublishArtifact "$buildDir\CouchDude.Bootstrapper.$version.nupkg"
    
    prepareAndPackage -templateNuSpec "$srcDir\Azure\CouchDude.Bootstrapper.Azure.nuspec" -fileTemplates ("$srcDir\Azure\bin\$config\CouchDude.Bootstrapper.Azure.*")
    TeamCity-PublishArtifact "$buildDir\CouchDude.Bootstrapper.Azure.$version.nupkg"
}