# StarDictNet

Rudimentary class lib that reads StarDict dictionaries.
Also, a Powershell module which acts as a very basic cli StarDict dictionary lookup program.

# Using Powershell Module

- Add full path of *.ifo files of dictionaries to global variables:
```powershell
$Global:Dict1 = Set-Dict "full/path/to/somedict.ifo"
```
- Optionally, add them to arrays in order to create dictionary groups:
```powershell
$Global:English = @($Dict1, $Dict2, $Dict3, $Dict4)
$Global:French = @($Dict5, $Dict6, $Dict7)
```

- At lines 100 and 154 add full path of **elinks** if it is not in the **PATH**. elinks enable us to display HTML nicely on the terminal.

- Put StarDict.psm1 and StarDictNet.dll right next to your Microsoft.PowerShell_profile.ps1

- Add these 2 lines to your Microsoft.PowerShell_profile.ps1:
```powershell
Import-Module $PSScriptRoot\StarDict.psm1
Set-DictSession
```

* Lookup words:
    - Lookup at a single dictionary:
    ```powershell
    $Dict1 | Get-Def Beleaguered -IgnoreCase <# will match beleaguered also #>
    $Dict1 | Get-DefRegex "^beleag*" <# get words that starts with beleag #>
    $FrenchDict1 | Get-Def "eutes" -IgnoreDiacritics  <# instead of eûtes #>
    ```
    - Lookup at multiple dictionaries:
    ````powershell
    $English | Get-BatchDef "daredevil"
    $Turkce | Get-BatchDefRegex "^\b(\w+)\s\1$" <# search reduplicative words with regex #>
    ````
# Screenshots
![Ignore diacritics](/img/ignore_diacritics.png)
*Ignore diacritics while searching.*

![Batch lookup](/img/daredevil.png)
*Batch lookup*

![Query with regular expressions](/img/regex.png)
*Query with regular expressions*

# Note
Windows elinks build is provided as-is, use at your own risk.
