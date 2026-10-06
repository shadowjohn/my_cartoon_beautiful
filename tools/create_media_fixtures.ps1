[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtures = Join-Path $repo 'artifacts\media-tests\fixtures'
$baseline = Join-Path $repo 'artifacts\media-tests\baseline'
$ffmpeg = Join-Path $repo 'my_cartoon_beautiful\binary\ffmpeg.exe'
$esrgan = Join-Path $repo 'my_cartoon_beautiful\binary\realesrgan-ncnn-vulkan-v0.2.0-windows\realesrgan-ncnn-vulkan.exe'
$results = [Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path $fixtures,$baseline -Force | Out-Null

function Invoke-LegacyTool {
    param([string]$Name, [string]$FileName, [string[]]$Arguments, [string]$OutputFile = '', [int]$TimeoutSeconds = 60)
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FileName
    $info.WorkingDirectory = Split-Path -Parent $FileName
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $watch = [Diagnostics.Stopwatch]::StartNew()
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $watch.Stop()
    $entry = [ordered]@{
        name = $Name
        fileName = $FileName
        arguments = $Arguments
        exitCode = $process.ExitCode
        timedOut = $timedOut
        elapsedMs = $watch.ElapsedMilliseconds
        outputFile = $OutputFile
        outputBytes = if ($OutputFile -and (Test-Path -LiteralPath $OutputFile)) { (Get-Item -LiteralPath $OutputFile).Length } else { $null }
        stdoutLog = Join-Path $baseline "$Name.stdout.log"
        stderrLog = Join-Path $baseline "$Name.stderr.log"
    }
    [IO.File]::WriteAllText($entry.stdoutLog, $stdout)
    [IO.File]::WriteAllText($entry.stderrLog, $stderr)
    $results.Add([pscustomobject]$entry)
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $baseline 'commands-and-results.json') -Encoding utf8
    Write-Output ("{0}: exit={1}; timeout={2}; ms={3}; bytes={4}" -f $Name,$entry.exitCode,$timedOut,$watch.ElapsedMilliseconds,$entry.outputBytes)
    $process.Dispose()
}

Invoke-LegacyTool 'ffmpeg-version' $ffmpeg @('-version')
foreach ($rate in @(30,24)) {
    $output = Join-Path $fixtures "sync-${rate}fps.mp4"
    if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite fixture: $output" }
    $flashEnd = if ($rate -eq 30) { '1.032' } else { '1.040' }
    $video = "testsrc2=size=96x64:rate=${rate}:duration=2,drawbox=x=0:y=0:w=iw:h=ih:color=white:t=fill:enable='between(t,1,$flashEnd)'"
    $audio = "aevalsrc=exprs='if(between(t,1,1.01),0.8*sin(2*PI*1000*(t-1)),0)':s=48000:d=2"
    Invoke-LegacyTool "fixture-$rate" $ffmpeg @('-hide_banner','-nostdin','-n','-f','lavfi','-i',$video,'-f','lavfi','-i',$audio,'-c:v','mpeg4','-q:v','2','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-shortest',$output) $output
}
$source = Join-Path $fixtures 'sync-30fps.mp4'
$vfr = Join-Path $fixtures 'sync-vfr.mp4'
Invoke-LegacyTool 'fixture-vfr' $ffmpeg @('-hide_banner','-nostdin','-n','-i',$source,'-vf',"select='not(mod(n,2))+eq(mod(n,5),1)'",'-fps_mode','vfr','-c:v','mpeg4','-q:v','2','-c:a','copy',$vfr) $vfr
$silent = Join-Path $fixtures 'sync-no-audio.mp4'
Invoke-LegacyTool 'fixture-no-audio' $ffmpeg @('-hide_banner','-nostdin','-n','-i',$source,'-an','-c:v','copy',$silent) $silent

foreach ($fixture in @('sync-30fps','sync-24fps','sync-vfr','sync-no-audio')) {
    $inputFile = Join-Path $fixtures "$fixture.mp4"
    $frameDirectory = Join-Path $baseline "$fixture-frames"
    New-Item -ItemType Directory -Path $frameDirectory -Force | Out-Null
    $framePattern = Join-Path $frameDirectory '%08d.png'
    Invoke-LegacyTool "$fixture-extract" $ffmpeg @('-hide_banner','-nostdin','-n','-hwaccel','auto','-i',$inputFile,'-vf','fps=30','-f','image2',$framePattern)
    Invoke-LegacyTool "$fixture-probe" $ffmpeg @('-hide_banner','-nostdin','-i',$inputFile,'-vf','showinfo','-an','-f','null','-')
}

$formats = @(
    @{ Name='aac'; Extension='aac'; Options=@('-c:a','aac','-b:a','192k') },
    @{ Name='mp3'; Extension='mp3'; Options=@('-c:a','libmp3lame','-q:a','4') },
    @{ Name='vorbis'; Extension='ogg'; Options=@('-c:a','libvorbis','-q:a','4') },
    @{ Name='pcm'; Extension='wav'; Options=@('-c:a','pcm_s16le') }
)
$framePattern = Join-Path $baseline 'sync-30fps-frames\%08d.png'
foreach ($format in $formats) {
    $audioFile = Join-Path $baseline ("audio-{0}.{1}" -f $format.Name,$format.Extension)
    $arguments = @('-hide_banner','-nostdin','-n','-hwaccel','auto','-i',$source,'-vn') + $format.Options + @($audioFile)
    Invoke-LegacyTool ("audio-{0}-extract" -f $format.Name) $ffmpeg $arguments $audioFile
    $muxFile = Join-Path $baseline ("mux-{0}.mp4" -f $format.Name)
    Invoke-LegacyTool ("audio-{0}-mux" -f $format.Name) $ffmpeg @('-hide_banner','-nostdin','-n','-hwaccel','auto','-framerate','30','-i',$framePattern,'-i',$audioFile,'-c:v','libopenh264','-pix_fmt','yuv420p','-acodec','copy',$muxFile) $muxFile
    Invoke-LegacyTool ("audio-{0}-decode" -f $format.Name) $ffmpeg @('-hide_banner','-nostdin','-i',$muxFile,'-f','null','-')
}
$absentAudio = Join-Path $baseline 'no-audio.aac'
Invoke-LegacyTool 'no-audio-extract' $ffmpeg @('-hide_banner','-nostdin','-n','-i',$silent,'-vn','-c:a','aac','-b:a','192k',$absentAudio) $absentAudio

$imageFile = Join-Path $baseline 'sync-30fps-frames\00000001.png'
foreach ($scale in @(2,3,4)) {
    $output = Join-Path $baseline "esrgan-x$scale.png"
    Invoke-LegacyTool "esrgan-x$scale" $esrgan @('-i',$imageFile,'-o',$output,'-s',[string]$scale,'-f','png') $output -TimeoutSeconds 120
}

Add-Type -AssemblyName System.Drawing
$dimensions = foreach ($scale in @(2,3,4)) {
    $output = Join-Path $baseline "esrgan-x$scale.png"
    if (Test-Path -LiteralPath $output) {
        $bitmap = [Drawing.Image]::FromFile($output)
        [pscustomobject]@{ file=$output; width=$bitmap.Width; height=$bitmap.Height }
        $bitmap.Dispose()
    }
}
$dimensions | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $baseline 'esrgan-dimensions.json') -Encoding utf8
$frameCounts = foreach ($fixture in @('sync-30fps','sync-24fps','sync-vfr','sync-no-audio')) {
    $directory = Join-Path $baseline "$fixture-frames"
    [pscustomobject]@{ fixture=$fixture; count=@(Get-ChildItem -LiteralPath $directory -Filter '*.png').Count }
}
$frameCounts | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $baseline 'frame-counts.json') -Encoding utf8
Write-Output 'Legacy baseline finished.'

# Additional timing/geometry fixtures (all generated under artifacts).
$ErrorActionPreference='Stop'
$ffmpeg=Join-Path $repo 'my_cartoon_beautiful/binary/ffmpeg.exe'
$source='artifacts/media-tests/fixtures/sync-30fps.mp4'
& $ffmpeg -hide_banner -loglevel error -y -display_rotation:v:0 90 -i $source -c copy artifacts/media-tests/fixtures/sync-rotate.mp4
if($LASTEXITCODE -ne 0){throw 'rotate fixture'}
& $ffmpeg -hide_banner -loglevel error -y -i $source -c copy -output_ts_offset 2 artifacts/media-tests/fixtures/sync-offset.mp4
if($LASTEXITCODE -ne 0){throw 'offset fixture'}
& $ffmpeg -hide_banner -loglevel error -y -i $source -c:v mpeg4 -bf 2 -q:v 2 -c:a copy artifacts/media-tests/fixtures/sync-bframes.mp4
if($LASTEXITCODE -ne 0){throw 'bframes fixture'}
& $ffmpeg -hide_banner -loglevel error -y -i $source -vf setsar=4/3 -c:v mpeg4 -q:v 2 -c:a copy artifacts/media-tests/fixtures/sync-sar.mp4
if($LASTEXITCODE -ne 0){throw 'sar fixture'}
