param([Parameter(Mandatory=$true)][string]$NcnnSource)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$patches=Get-Content -LiteralPath (Join-Path $repo 'native/patches/manifest.json') -Raw | ConvertFrom-Json
foreach($patch in $patches){
    $path=Join-Path $NcnnSource $patch.source
    $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if($hash -eq $patch.patchedSha256){continue}
    if($hash -ne $patch.originalSha256){throw "Unexpected pinned source: $($patch.source)"}
    $original=[Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path))
    $newline=if($original.Contains("`r`n")){"`r`n"}else{"`n"}
    $lines=[Collections.Generic.List[string]]::new()
    $lines.AddRange([string[]]$original.Replace("`r`n","`n").Split("`n"))
    $diff=[IO.File]::ReadAllText((Join-Path $repo $patch.patch)).Replace("`r`n","`n").Split("`n")
    $offset=0
    for($i=0;$i -lt $diff.Length;$i++){
        if($diff[$i] -notmatch '^@@ -(\d+)(?:,\d+)? \+\d+(?:,\d+)? @@'){continue}
        $index=[int]$Matches[1]-1+$offset
        $remove=[Collections.Generic.List[string]]::new()
        $insert=[Collections.Generic.List[string]]::new()
        for($i++;$i -lt $diff.Length -and !$diff[$i].StartsWith('@@');$i++){
            $line=$diff[$i]
            if(!$line.Length){continue}
            if($line[0] -eq ' ' -or $line[0] -eq '-'){$remove.Add($line.Substring(1))}
            if($line[0] -eq ' ' -or $line[0] -eq '+'){$insert.Add($line.Substring(1))}
        }
        $i--
        for($j=0;$j -lt $remove.Count;$j++){
            if($lines[$index+$j] -cne $remove[$j]){throw "Patch context mismatch: $($patch.source) line $($index+$j+1)"}
        }
        $lines.RemoveRange($index,$remove.Count)
        $lines.InsertRange($index,$insert)
        $offset+=$insert.Count-$remove.Count
    }
    $bytes=[Text.Encoding]::UTF8.GetBytes([string]::Join($newline,$lines))
    $sha=[Security.Cryptography.SHA256]::Create()
    try{$actual=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','')}finally{$sha.Dispose()}
    if($actual -ne $patch.patchedSha256){throw "Patched digest mismatch: $($patch.source)"}
    [IO.File]::WriteAllBytes($path,$bytes)
}
