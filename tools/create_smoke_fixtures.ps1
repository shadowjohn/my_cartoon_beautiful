[CmdletBinding()]
param([switch]$IncludeEndurance)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtures=Join-Path $repo 'artifacts/media-tests/fixtures'
$ffmpeg=Join-Path $repo 'my_cartoon_beautiful/binary/ffmpeg.exe'
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
function Run-Fixture([string[]]$Arguments){ & $ffmpeg -hide_banner -loglevel error -nostdin -y @Arguments; if($LASTEXITCODE -ne 0){throw 'Fixture generation failed'} }
foreach($rate in @(30,24)) {
    $flashEnd=if($rate -eq 30){'1.032'}else{'1.040'}
    $video="testsrc2=size=96x64:rate=${rate}:duration=2,drawbox=x=0:y=0:w=iw:h=ih:color=white:t=fill:enable='between(t,1,$flashEnd)'"
    $audio="aevalsrc=exprs='if(between(t,1,1.01),0.8*sin(2*PI*1000*(t-1)),0)':s=48000:d=2"
    Run-Fixture @('-f','lavfi','-i',$video,'-f','lavfi','-i',$audio,'-c:v','mpeg4','-q:v','2','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-shortest',(Join-Path $fixtures "sync-${rate}fps.mp4"))
}
$source=Join-Path $fixtures 'sync-30fps.mp4'
Run-Fixture @('-i',$source,'-vf',"select='not(mod(n,2))+eq(mod(n,5),1)'",'-fps_mode','vfr','-c:v','mpeg4','-q:v','2','-c:a','copy',(Join-Path $fixtures 'sync-vfr.mp4'))
Run-Fixture @('-i',$source,'-an','-c:v','copy',(Join-Path $fixtures 'sync-no-audio.mp4'))
Run-Fixture @('-display_rotation:v:0','90','-i',$source,'-c','copy',(Join-Path $fixtures 'sync-rotate.mp4'))
Run-Fixture @('-i',$source,'-c','copy','-output_ts_offset','2',(Join-Path $fixtures 'sync-offset.mp4'))
Run-Fixture @('-i',$source,'-c:v','mpeg4','-bf','2','-q:v','2','-c:a','copy',(Join-Path $fixtures 'sync-bframes.mp4'))
Run-Fixture @('-i',$source,'-vf','setsar=4/3','-c:v','mpeg4','-q:v','2','-c:a','copy',(Join-Path $fixtures 'sync-sar.mp4'))
if($IncludeEndurance) {
    $video="color=c=black:s=96x64:r=30:d=600,drawbox=x=0:y=0:w=iw:h=ih:color=white:t=fill:enable='lt(mod(t,1),0.033)'"
    $audio="aevalsrc=exprs='if(lt(mod(t,1),0.01),0.8*sin(2*PI*1000*t),0)':s=48000:d=600"
    Run-Fixture @('-f','lavfi','-i',$video,'-f','lavfi','-i',$audio,'-c:v','mpeg4','-q:v','2','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-shortest',(Join-Path $fixtures 'endurance-10min.mp4'))
}
Write-Output "CPU fixtures ready: $fixtures (development CLI only; not packaged)"
