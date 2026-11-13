param([string]$Source,[string]$Output)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(!$Source){$Source=Join-Path $taskRoot 'assets/icons/icon.png'}
if(!$Output){$Output=Join-Path $taskRoot 'assets/icons/icon.ico'}
Add-Type -AssemblyName System.Drawing
$original=[System.Drawing.Image]::FromFile($Source)
$sizes=@(16,24,32,48,64,128,256)
$images=@()
try {
    foreach($size in $sizes){
        $bitmap=New-Object System.Drawing.Bitmap($size,$size)
        $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scale=[Math]::Min($size/$original.Width,$size/$original.Height)
            $graphics.DrawImage($original,[single](($size-$original.Width*$scale)/2),[single](($size-$original.Height*$scale)/2),[single]($original.Width*$scale),[single]($original.Height*$scale))
            $stream=New-Object System.IO.MemoryStream
            try {$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png);$images+=,@($stream.ToArray())}finally{$stream.Dispose()}
        }finally{$graphics.Dispose();$bitmap.Dispose()}
    }
    $file=[System.IO.File]::Create($Output)
    $writer=New-Object System.IO.BinaryWriter($file)
    try {
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
        $offset=6+16*$sizes.Count
        for($i=0;$i -lt $sizes.Count;$i++){
            $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
            $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
            $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset)
            $offset+=$images[$i].Length
        }
        foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
    }finally{$writer.Dispose();$file.Dispose()}
}finally{$original.Dispose()}
Get-Item -LiteralPath $Output | Select-Object FullName,Length
