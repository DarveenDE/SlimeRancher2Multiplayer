param(
    [string[]]$Assembly = @("Assembly-CSharp.dll", "SR2E.dll"),
    [string]$OutputDir = "",
    [switch]$SyncRefs,
    [switch]$IncludePrivate,
    [switch]$IncludeCompilerGenerated
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "DevSettings.ps1")

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = $Script:ReferenceDumpDir
}

if ($SyncRefs) {
    & (Join-Path $PSScriptRoot "Sync-GameRefs.ps1")
}

if (-not (Test-Path -LiteralPath $Script:LibrariesDir)) {
    throw "Reference DLL folder was not found at $Script:LibrariesDir. Run tools\Sync-GameRefs.ps1 first."
}

$cecilPath = Join-Path $Script:LibrariesDir "Mono.Cecil.dll"
if (-not (Test-Path -LiteralPath $cecilPath)) {
    throw "Mono.Cecil.dll was not found at $cecilPath. Run tools\Sync-GameRefs.ps1 first."
}

Add-Type -Path $cecilPath

$apiOutputDir = Join-Path $OutputDir "api"
New-Item -ItemType Directory -Force -Path $apiOutputDir | Out-Null

$typeNameAliases = @{
    "System.Void" = "void"
    "System.Boolean" = "bool"
    "System.Byte" = "byte"
    "System.SByte" = "sbyte"
    "System.Char" = "char"
    "System.Decimal" = "decimal"
    "System.Double" = "double"
    "System.Single" = "float"
    "System.Int32" = "int"
    "System.UInt32" = "uint"
    "System.Int64" = "long"
    "System.UInt64" = "ulong"
    "System.Int16" = "short"
    "System.UInt16" = "ushort"
    "System.Object" = "object"
    "System.String" = "string"
    "Il2CppSystem.Void" = "void"
    "Il2CppSystem.Boolean" = "bool"
    "Il2CppSystem.Byte" = "byte"
    "Il2CppSystem.SByte" = "sbyte"
    "Il2CppSystem.Char" = "char"
    "Il2CppSystem.Decimal" = "decimal"
    "Il2CppSystem.Double" = "double"
    "Il2CppSystem.Single" = "float"
    "Il2CppSystem.Int32" = "int"
    "Il2CppSystem.UInt32" = "uint"
    "Il2CppSystem.Int64" = "long"
    "Il2CppSystem.UInt64" = "ulong"
    "Il2CppSystem.Int16" = "short"
    "Il2CppSystem.UInt16" = "ushort"
    "Il2CppSystem.Object" = "object"
    "Il2CppSystem.String" = "string"
}

function Add-Line {
    param(
        [Parameter(Mandatory=$true)][System.Text.StringBuilder]$Builder,
        [string]$Line = ""
    )

    [void]$Builder.AppendLine($Line)
}

function Format-CecilTypeName {
    param([Mono.Cecil.TypeReference]$Type)

    if ($null -eq $Type) {
        return "void"
    }

    if ($Type -is [Mono.Cecil.ArrayType]) {
        $arrayType = [Mono.Cecil.ArrayType]$Type
        $rankSuffix = if ($arrayType.Rank -le 1) { "[]" } else { "[" + ("," * ($arrayType.Rank - 1)) + "]" }
        return "$(Format-CecilTypeName $arrayType.ElementType)$rankSuffix"
    }

    if ($Type -is [Mono.Cecil.ByReferenceType]) {
        $byRefType = [Mono.Cecil.ByReferenceType]$Type
        return "$(Format-CecilTypeName $byRefType.ElementType)&"
    }

    if ($Type -is [Mono.Cecil.PointerType]) {
        $pointerType = [Mono.Cecil.PointerType]$Type
        return "$(Format-CecilTypeName $pointerType.ElementType)*"
    }

    if ($Type -is [Mono.Cecil.GenericParameter]) {
        return $Type.Name
    }

    if ($Type -is [Mono.Cecil.GenericInstanceType]) {
        $genericType = [Mono.Cecil.GenericInstanceType]$Type
        $baseName = $genericType.ElementType.FullName
        $baseName = $baseName -replace "``\d+$", ""
        $baseName = $baseName -replace "/", "."
        $arguments = @($genericType.GenericArguments | ForEach-Object { Format-CecilTypeName $_ })
        return "$baseName<$($arguments -join ', ')>"
    }

    $fullName = $Type.FullName
    if ($typeNameAliases.ContainsKey($fullName)) {
        return $typeNameAliases[$fullName]
    }

    $fullName = $fullName -replace "/", "."
    $fullName = $fullName -replace "``\d+", ""

    if ($Type.HasGenericParameters) {
        $arguments = @($Type.GenericParameters | ForEach-Object { $_.Name })
        return "$fullName<$($arguments -join ', ')>"
    }

    return $fullName
}

function Get-TypeVisibility {
    param([Mono.Cecil.TypeDefinition]$Type)

    if ($Type.IsPublic -or $Type.IsNestedPublic) { return "public" }
    if ($Type.IsNestedFamily) { return "protected" }
    if ($Type.IsNestedFamilyOrAssembly) { return "protected internal" }
    if ($Type.IsNestedAssembly -or $Type.IsNotPublic) { return "internal" }
    if ($Type.IsNestedPrivate) { return "private" }
    return "private"
}

function Get-MemberVisibility {
    param($Member)

    if ($Member.IsPublic) { return "public" }
    if ($Member.IsFamily) { return "protected" }
    if ($Member.IsFamilyOrAssembly) { return "protected internal" }
    if ($Member.IsAssembly) { return "internal" }
    return "private"
}

function Test-TypeIncluded {
    param([Mono.Cecil.TypeDefinition]$Type)

    if ($IncludePrivate) {
        return $true
    }

    return $Type.IsPublic -or $Type.IsNestedPublic -or $Type.IsNestedFamily -or $Type.IsNestedFamilyOrAssembly
}

function Test-MemberIncluded {
    param($Member)

    if ($IncludePrivate) {
        return $true
    }

    return $Member.IsPublic -or $Member.IsFamily -or $Member.IsFamilyOrAssembly
}

function Test-PropertyIncluded {
    param([Mono.Cecil.PropertyDefinition]$Property)

    return (($Property.GetMethod -and (Test-MemberIncluded $Property.GetMethod)) -or
            ($Property.SetMethod -and (Test-MemberIncluded $Property.SetMethod)))
}

function Test-GeneratedMemberName {
    param([string]$Name)

    return $Name -eq "NativeClassPtr" -or
           $Name -eq "Pointer" -or
           $Name -like "NativeFieldInfoPtr_*" -or
           $Name -like "NativeMethodInfoPtr_*" -or
           $Name -like "NativePropertyInfoPtr_*" -or
           $Name -like "NativeFieldInfoPtr*" -or
           $Name -like "NativeMethodInfoPtr*" -or
           $Name -like "NativePropertyInfoPtr*" -or
           $Name -like "__*" -or
           $Name -like "<*>*" -or
           $Name -like "*_b__*" -or
           $Name -like "*k__BackingField"
}

function Test-GeneratedTypeName {
    param([Mono.Cecil.TypeDefinition]$Type)

    return $Type.Name -like "__c*" -or
           $Type.Name -like "<*>*" -or
           $Type.Name -like "*_d__*" -or
           $Type.FullName -like "*.__c" -or
           $Type.FullName -like "*.__c*" -or
           $Type.FullName -like "*_d__*" -or
           $Type.FullName -like "*.<*>*"
}

function Get-TypeKind {
    param([Mono.Cecil.TypeDefinition]$Type)

    if ($Type.IsInterface) { return "interface" }
    if ($Type.IsEnum) { return "enum" }
    if ($Type.IsValueType) { return "struct" }
    if ($Type.BaseType -and $Type.BaseType.FullName -in @("System.MulticastDelegate", "Il2CppSystem.MulticastDelegate")) {
        return "delegate"
    }
    return "class"
}

function Get-AllTypes {
    param([System.Collections.IEnumerable]$Types)

    foreach ($type in $Types) {
        $type

        if ($type.HasNestedTypes) {
            Get-AllTypes $type.NestedTypes
        }
    }
}

function Format-FieldSignature {
    param([Mono.Cecil.FieldDefinition]$Field)

    $tokens = New-Object System.Collections.Generic.List[string]
    $tokens.Add((Get-MemberVisibility $Field))
    if ($Field.IsStatic) { $tokens.Add("static") }
    if ($Field.IsInitOnly) { $tokens.Add("readonly") }
    if ($Field.IsLiteral) { $tokens.Add("const") }
    $tokens.Add((Format-CecilTypeName $Field.FieldType))
    $tokens.Add($Field.Name)

    $signature = $tokens -join " "
    if ($Field.IsLiteral -and $null -ne $Field.Constant) {
        $signature += " = $($Field.Constant)"
    }

    return $signature
}

function Format-PropertySignature {
    param([Mono.Cecil.PropertyDefinition]$Property)

    $visibility = if ($Property.GetMethod) { Get-MemberVisibility $Property.GetMethod } else { Get-MemberVisibility $Property.SetMethod }
    $accessors = New-Object System.Collections.Generic.List[string]

    if ($Property.GetMethod -and (Test-MemberIncluded $Property.GetMethod)) {
        $accessors.Add("get;")
    }

    if ($Property.SetMethod -and (Test-MemberIncluded $Property.SetMethod)) {
        $accessors.Add("set;")
    }

    return "$visibility $(Format-CecilTypeName $Property.PropertyType) $($Property.Name) { $($accessors -join ' ') }"
}

function Format-ParameterSignature {
    param([Mono.Cecil.ParameterDefinition]$Parameter)

    $prefix = ""
    $type = $Parameter.ParameterType

    if ($Parameter.IsOut) {
        $prefix = "out "
    } elseif ($type -is [Mono.Cecil.ByReferenceType]) {
        $prefix = "ref "
        $type = ([Mono.Cecil.ByReferenceType]$type).ElementType
    }

    $name = if ([string]::IsNullOrWhiteSpace($Parameter.Name)) { "arg$($Parameter.Index)" } else { $Parameter.Name }
    return "$prefix$(Format-CecilTypeName $type) $name"
}

function Format-MethodSignature {
    param([Mono.Cecil.MethodDefinition]$Method)

    $tokens = New-Object System.Collections.Generic.List[string]
    $tokens.Add((Get-MemberVisibility $Method))

    if ($Method.IsStatic) { $tokens.Add("static") }
    if ($Method.IsAbstract -and -not $Method.DeclaringType.IsInterface) { $tokens.Add("abstract") }
    elseif ($Method.IsVirtual -and -not $Method.IsFinal -and -not $Method.DeclaringType.IsInterface) { $tokens.Add("virtual") }

    $name = $Method.Name
    $isConstructor = $name -eq ".ctor"
    if ($isConstructor) {
        $name = $Method.DeclaringType.Name -replace "``\d+$", ""
    } else {
        $tokens.Add((Format-CecilTypeName $Method.ReturnType))
    }

    if ($Method.HasGenericParameters) {
        $genericArgs = @($Method.GenericParameters | ForEach-Object { $_.Name })
        $name += "<$($genericArgs -join ', ')>"
    }

    $parameters = @($Method.Parameters | ForEach-Object { Format-ParameterSignature $_ })
    return "$($tokens -join ' ') $name($($parameters -join ', '))"
}

function Resolve-AssemblyInputs {
    param([string[]]$AssemblyInputs)

    $resolved = New-Object System.Collections.Generic.List[string]

    foreach ($assemblyInput in $AssemblyInputs) {
        $inputHasWildcard = $assemblyInput.Contains("*") -or $assemblyInput.Contains("?")
        $inputLooksLikePath = [System.IO.Path]::IsPathRooted($assemblyInput) -or
                              $assemblyInput.Contains("\") -or
                              $assemblyInput.Contains("/")

        if ($inputHasWildcard) {
            $searchDir = if ($inputLooksLikePath) { Split-Path -Parent $assemblyInput } else { $Script:LibrariesDir }
            $filter = Split-Path -Leaf $assemblyInput
            if ([string]::IsNullOrWhiteSpace($searchDir)) {
                $searchDir = $Script:LibrariesDir
            }

            Get-ChildItem -Path $searchDir -Filter $filter -File | ForEach-Object {
                $resolved.Add($_.FullName)
            }
            continue
        }

        $assemblyPath = if ($inputLooksLikePath) { $assemblyInput } else { Join-Path $Script:LibrariesDir $assemblyInput }
        if (-not (Test-Path -LiteralPath $assemblyPath)) {
            Write-Warning "Skipping missing assembly: $assemblyInput"
            continue
        }

        $resolved.Add((Resolve-Path -LiteralPath $assemblyPath).Path)
    }

    return @($resolved | Sort-Object -Unique)
}

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($Script:LibrariesDir)

$readerParameters = New-Object Mono.Cecil.ReaderParameters
$readerParameters.AssemblyResolver = $resolver
$readerParameters.ReadSymbols = $false
$readerParameters.InMemory = $true

$assemblyFiles = Resolve-AssemblyInputs $Assembly
if ($assemblyFiles.Count -eq 0) {
    throw "No assemblies matched the requested input: $($Assembly -join ', ')"
}

$manifestAssemblies = New-Object System.Collections.Generic.List[object]

foreach ($assemblyFile in $assemblyFiles) {
    Write-Host "Dumping API metadata from $assemblyFile"

    $assemblyDefinition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyFile, $readerParameters)
    $assemblyName = $assemblyDefinition.Name.Name
    $safeAssemblyName = $assemblyName -replace "[^A-Za-z0-9_.-]", "_"
    $allTypes = @(Get-AllTypes $assemblyDefinition.MainModule.Types | Where-Object {
        $_.FullName -ne "<Module>" -and
        $_.FullName -notlike "<PrivateImplementationDetails>*" -and
        ($IncludeCompilerGenerated -or -not (Test-GeneratedTypeName $_)) -and
        (Test-TypeIncluded $_)
    } | Sort-Object FullName)

    $markdown = New-Object System.Text.StringBuilder
    Add-Line $markdown "# $assemblyName public API"
    Add-Line $markdown ""
    Add-Line $markdown ('- Source: `{0}`' -f $assemblyFile)
    Add-Line $markdown "- Generated: $([DateTimeOffset]::UtcNow.ToString('u'))"
    Add-Line $markdown "- Include private members: $IncludePrivate"
    Add-Line $markdown "- Include compiler-generated types: $IncludeCompilerGenerated"
    Add-Line $markdown "- Type count: $($allTypes.Count)"
    Add-Line $markdown ""

    $references = @($assemblyDefinition.MainModule.AssemblyReferences | Sort-Object Name)
    if ($references.Count -gt 0) {
        Add-Line $markdown "## Assembly references"
        Add-Line $markdown ""
        foreach ($reference in $references) {
            Add-Line $markdown "- $($reference.Name) $($reference.Version)"
        }
        Add-Line $markdown ""
    }

    $typeRows = New-Object System.Collections.Generic.List[object]
    $namespaceGroups = $allTypes | Group-Object Namespace | Sort-Object Name

    foreach ($namespaceGroup in $namespaceGroups) {
        $namespaceName = if ([string]::IsNullOrWhiteSpace($namespaceGroup.Name)) { "(global)" } else { $namespaceGroup.Name }
        Add-Line $markdown "## $namespaceName"
        Add-Line $markdown ""

        foreach ($type in ($namespaceGroup.Group | Sort-Object Name)) {
            $kind = Get-TypeKind $type
            $visibility = Get-TypeVisibility $type
            $typeName = Format-CecilTypeName $type
            $baseType = if ($type.BaseType -and -not $type.IsEnum -and -not $type.IsInterface) { Format-CecilTypeName $type.BaseType } else { "" }

            Add-Line $markdown "### $visibility $kind $typeName"
            if (-not [string]::IsNullOrWhiteSpace($baseType)) {
                Add-Line $markdown ""
                Add-Line $markdown ('Base: `{0}`' -f $baseType)
            }

            if ($type.HasInterfaces) {
                $interfaces = @($type.Interfaces | ForEach-Object { Format-CecilTypeName $_.InterfaceType } | Sort-Object)
                if ($interfaces.Count -gt 0) {
                    Add-Line $markdown ""
                    Add-Line $markdown ('Implements: `{0}`' -f ($interfaces -join '`, `'))
                }
            }

            $fields = if ($type.IsEnum) {
                @()
            } else {
                @($type.Fields | Where-Object {
                    (Test-MemberIncluded $_) -and -not (Test-GeneratedMemberName $_.Name)
                } | Sort-Object Name)
            }

            $properties = @($type.Properties | Where-Object {
                (Test-PropertyIncluded $_) -and -not (Test-GeneratedMemberName $_.Name)
            } | Sort-Object Name)

            $methods = @($type.Methods | Where-Object {
                (Test-MemberIncluded $_) -and
                $_.Name -ne ".cctor" -and
                ($_.Name -eq ".ctor" -or -not $_.IsSpecialName) -and
                -not (Test-GeneratedMemberName $_.Name)
            } | Sort-Object Name)

            if ($type.IsEnum) {
                $enumValues = @($type.Fields | Where-Object { $_.IsLiteral } | Sort-Object Constant)
                if ($enumValues.Count -gt 0) {
                    Add-Line $markdown ""
                    Add-Line $markdown "Enum values:"
                    Add-Line $markdown ""
                    foreach ($field in $enumValues) {
                        Add-Line $markdown ('- `{0} = {1}`' -f $field.Name, $field.Constant)
                    }
                }
            }

            if ($fields.Count -gt 0) {
                Add-Line $markdown ""
                Add-Line $markdown "Fields:"
                Add-Line $markdown ""
                foreach ($field in $fields) {
                    Add-Line $markdown ('- `{0}`' -f (Format-FieldSignature $field))
                }
            }

            if ($properties.Count -gt 0) {
                Add-Line $markdown ""
                Add-Line $markdown "Properties:"
                Add-Line $markdown ""
                foreach ($property in $properties) {
                    Add-Line $markdown ('- `{0}`' -f (Format-PropertySignature $property))
                }
            }

            if ($methods.Count -gt 0) {
                Add-Line $markdown ""
                Add-Line $markdown "Methods:"
                Add-Line $markdown ""
                foreach ($method in $methods) {
                    Add-Line $markdown ('- `{0}`' -f (Format-MethodSignature $method))
                }
            }

            Add-Line $markdown ""

            $typeRows.Add([ordered]@{
                namespace = $type.Namespace
                name = $type.Name
                fullName = $type.FullName
                visibility = $visibility
                kind = $kind
                baseType = $baseType
                fieldCount = $fields.Count
                propertyCount = $properties.Count
                methodCount = $methods.Count
            })
        }
    }

    $markdownPath = Join-Path $apiOutputDir "$safeAssemblyName.public-api.md"
    $jsonPath = Join-Path $apiOutputDir "$safeAssemblyName.types.json"

    $markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding UTF8
    $typeRows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

    $manifestAssemblies.Add([ordered]@{
        name = $assemblyName
        version = $assemblyDefinition.Name.Version.ToString()
        source = $assemblyFile
        typeCount = $allTypes.Count
        markdown = $markdownPath
        typesJson = $jsonPath
    })
}

$manifest = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    repoRoot = $Script:RepoRoot
    librariesDir = $Script:LibrariesDir
    outputDir = $OutputDir
    includePrivate = [bool]$IncludePrivate
    includeCompilerGenerated = [bool]$IncludeCompilerGenerated
    assemblies = $manifestAssemblies
}

$manifestPath = Join-Path $OutputDir "manifest.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Reference dumps written to $OutputDir"
