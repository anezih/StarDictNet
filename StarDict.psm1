function Set-DictSession {
    try
    {
        $Global:Dict1 = Set-Dict "full/path/to/somedict.ifo"
        $Global:Dict7 = Set-Dict "full/path/to/somedict7.ifo"

        $Global:English = @($Dict1, $Dict2, $Dict3, $Dict4)
        $Global:French = @($Dict5, $Dict6, $Dict7)

    }
    catch
    {
        if($_.Exception.GetType() -ne [System.Management.Automation.MethodInvocationException])
        {
            Write-Warning "Couldn't create StarDictNet objects."
            Write-Host $_.ErrorDetails
        } 
    }
}

function Set-Dict {
    param (
        [string]$Path
    )
    Add-Type -AssemblyName $PSScriptRoot\StarDictNet.dll
    $fullPath = Resolve-Path -Path $Path
    $css = Get-CSS -full $fullPath
    
    return @{
        OBJ = [StarDictNet.StarDictNet]::new($fullPath)
        CSS = $css
    }
}

function Get-CSS {
    param (
        [string]$full
    )
    $parent = Split-Path $full -Parent
    $res = Join-Path -Path $parent -ChildPath "res"
    if (Test-Path -Path $res/*.css) {
        $css = ((Get-ChildItem $res -Filter *.css) | Sort-Object -Property Length -Descending | Select-Object -First 1 | Get-Content) -join ""
        return $css
    }
    else {
        return [String]::Empty
    }
}

function Add-ToHistory {
    param (
        [string]$Word
    )
    Add-Content -Path $PSScriptRoot\StarDictNet_history.txt -Value ("{0,-40}{1}" -f $Word, $(Get-Date))
}


function Get-Def {
    param (
        [Parameter(ValueFromPipeline)]
        $sdObjArr,

        [Parameter(Position=1)]
        [string]
        $Word,

        [Parameter(Position=2)]
        [switch]
        $IgnoreCase,

        [Parameter(Position=3)]
        [switch]
        $IgnoreDiacritics,

        [Parameter(Position=4)]
        [bool]
        $NoHistory = $false
    )
    begin {
        if (!$NoHistory) {
            Add-ToHistory -Word $Word
        }
    }
    process {
        $sdObj = $sdObjArr.OBJ
        $css = $sdObjArr.CSS

        $defs = $sdObj.GetDef($Word, $IgnoreCase, $IgnoreDiacritics)
        $html = ""
        foreach ($item in $defs) {
            $temp = "<span style=`"color:red;`">$($item.Item1)</span><br/>$($item.Item2)<hr/>"
            $html += $temp
        }
        if ($html.Length -gt 0) {
            if ($css.Length -gt 0) {
                $html = "<style>$($css)</style>$($html)"
            }
            $htmlName = "$($sdObj.Metadata.WordCount).html"
            $html > $htmlName
            & "elinks" --dump $htmlName --no-home 1 --dump-color-mode 4 --no-references --no-numbering
        }
        else {
            Write-Host "`n  `e[31;1;4m[!] No result`e[0m`n"
        }
    }
}

function Get-DefRegex {
    param (
        [Parameter(ValueFromPipeline)]
        $sdObjArr,

        [Parameter(Position=1)]
        [string]
        $Pattern,

        [Parameter(Position=2)]
        [switch]
        $IgnoreCase,

        [Parameter(Position=3)]
        [switch]
        $IgnoreDiacritics,

        [Parameter(Position=4)]
        [switch]
        $MatchSyns,

        [Parameter(Position=5)]
        [bool]
        $NoHistory
    )
    begin {
        if (!$NoHistory) {
            Add-ToHistory -Word $Word
        }
    }
    process {
        $sdObj = $sdObjArr.OBJ
        $css = $sdObjArr.CSS

        $defs = $sdObj.GetDefRegex($Pattern, !$IgnoreCase, $IgnoreDiacritics, $MatchSyns)
        $html = ""
        foreach ($item in $defs) {
            $temp = "<span style=`"color:red;`">$($item.Item1)</span><br/>$($item.Item2)<hr/>"
            $html += $temp
        }
        if ($html.Length -gt 0) {
            if ($css.Length -gt 0) {
                $html = "<style>$($css)</style>$($html)"
            }
            $htmlName = "$($sdObj.Metadata.WordCount).html"
            $html > $htmlName
            & "elinks" --dump $htmlName --no-home 1 --dump-color-mode 4 --no-references --no-numbering
        }
        else {
            Write-Host "`n  `e[31;1;4m[!] No result`e[0m`n"
        }
    }
}

function Get-BatchDef {
    param (
        [Parameter(ValueFromPipeline)]
        $Arr,

        [Parameter(Position=1)]
        [string]
        $Word,

        [Parameter(Position=2)]
        [switch]
        $IgnoreCase,

        [Parameter(Position=3)]
        [switch]
        $IgnoreDiacritics
    )
    begin {
        Add-ToHistory -Word $Word
    }
    process {
        foreach ($item in $Arr) {
            Write-Host "`e[33;1;4m>> $($item.OBJ.Metadata.BookName)`e[0m"
            $item | Get-Def -Word:$Word -IgnoreCase:$IgnoreCase -IgnoreDiacritics:$IgnoreDiacritics -NoHistory $true
        }
    }
}

function Get-BatchDefRegex {
    param (
        [Parameter(ValueFromPipeline)]
        $Arr,

        [Parameter(Position=1)]
        [string]
        $Pattern,

        [Parameter(Position=2)]
        [switch]
        $IgnoreCase,

        [Parameter(Position=3)]
        [switch]
        $IgnoreDiacritics,

        [Parameter(Position=4)]
        [switch]
        $MatchSyns
    )
    begin {
        Add-ToHistory -Word $Word
    }
    process {
        foreach ($item in $Arr) {
        Write-Host "`e[33;1;4m>> $($item.OBJ.Metadata.BookName)`e[0m"
        $item | Get-DefRegex -Pattern:$Pattern -IgnoreCase:$IgnoreCase -IgnoreDiacritics:$IgnoreDiacritics -MatchSyns:$MatchSyns -NoHistory $true
        }
    }
}

Export-ModuleMember -Function Set-Dict, Set-DictSession, Get-Def, Get-DefRegex, Get-BatchDef, Get-BatchDefRegex