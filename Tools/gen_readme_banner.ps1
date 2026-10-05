param([string]$Mode = 'preview')
# README banner for Samurai Run, drawn only from the game's own sprites:
# sky + bamboo + ground like the title screen, the SAMURAI RUN logo, and the cast standing on the ground
# (samurai running at a rock, an arrow and a shuriken in flight, an onigiri, the archer and the ninja).
# Output: banner.png, a 384x160 scene scaled 2x with hard pixel edges (768x320).
# Usage: powershell -File Tools\gen_readme_banner.ps1 preview   (writes to Temp\ReadmePreview)
#        powershell -File Tools\gen_readme_banner.ps1 write     (writes into Docs)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Proj = Split-Path -Parent $PSScriptRoot
$Art = Join-Path $Proj 'Assets\Art'
$PackSprites = Join-Path $Proj 'Assets\Assets\FREE_Samurai 2D Pixel Art v1.2\Sprites'
$OutDir = if ($Mode -eq 'write') { Join-Path $Proj 'Docs' } else { Join-Path $Proj 'Temp\ReadmePreview' }
New-Item -ItemType Directory -Force $OutDir | Out-Null

$CanvasW = 384; $CanvasH = 160; $GroundTop = 128; $OutScale = 2
$RunFrame = 3         # RUN.png has 16 frames of 96x96; frame 3 has the widest stride
$canvas = New-Object System.Drawing.Bitmap($CanvasW, $CanvasH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($canvas)
$gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$gfx.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$gfx.Clear([System.Drawing.Color]::FromArgb(255, 0x1A, 0x19, 0x32))

function LoadArt([string]$rel) { return [System.Drawing.Bitmap]::FromFile((Join-Path $Art $rel)) }

# Copy one cell out of a sheet, optionally mirrored left-right
function CutCell($sheet, [int]$cellX, [int]$cellY, [int]$cellW, [int]$cellH, [bool]$mirror) {
  $rect = New-Object System.Drawing.Rectangle($cellX, $cellY, $cellW, $cellH)
  $cell = $sheet.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  if ($mirror) { $cell.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX) }
  return $cell
}

# Bounding box of the visible pixels: minX, minY, maxX, maxY
function OpaqueBox($bmp) {
  $minX = $bmp.Width; $minY = $bmp.Height; $maxX = -1; $maxY = -1
  for ($py = 0; $py -lt $bmp.Height; $py++) {
    for ($px = 0; $px -lt $bmp.Width; $px++) {
      if ($bmp.GetPixel($px, $py).A -gt 0) {
        if ($px -lt $minX) { $minX = $px }
        if ($px -gt $maxX) { $maxX = $px }
        if ($py -lt $minY) { $minY = $py }
        if ($py -gt $maxY) { $maxY = $py }
      }
    }
  }
  return @($minX, $minY, $maxX, $maxY)
}

function DrawAt($bmp, [int]$destX, [int]$destY, [int]$scale) {
  $dest = New-Object System.Drawing.Rectangle($destX, $destY, ($bmp.Width * $scale), ($bmp.Height * $scale))
  $gfx.DrawImage($bmp, $dest, 0, 0, $bmp.Width, $bmp.Height, [System.Drawing.GraphicsUnit]::Pixel)
}

# Stand a sprite on the ground: lowest visible row just above $surface, visible center at $centerX
function StandOn($bmp, [int]$centerX, [int]$surface) {
  $box = OpaqueBox $bmp
  $destX = $centerX - [int][Math]::Floor(($box[0] + $box[2]) / 2)
  $destY = $surface - 1 - $box[3]
  DrawAt $bmp $destX $destY 1
}

# --- background: sky, two bamboo layers, ground (as in the title scene) ---
DrawAt (LoadArt 'Backgrounds\bg_sky.png') 0 0 1
$far = LoadArt 'Backgrounds\bg_bamboo_far.png'
for ($tx = 0; $tx -lt $CanvasW; $tx += $far.Width) { DrawAt $far $tx ($GroundTop + 6 - $far.Height) 1 }
$near = LoadArt 'Backgrounds\bg_bamboo_near.png'
for ($tx = -48; $tx -lt $CanvasW; $tx += $near.Width) { DrawAt $near $tx ($GroundTop + 10 - $near.Height) 1 }
$tile = LoadArt 'Backgrounds\ground_tile.png'
for ($tx = 0; $tx -lt $CanvasW; $tx += $tile.Width) { DrawAt $tile $tx $GroundTop 1 }
$Surface = $GroundTop + (OpaqueBox $tile)[1]

# --- logo ---
$logo = LoadArt 'UI\title_logo.png'
DrawAt $logo ([int][Math]::Floor(($CanvasW - $logo.Width * 2) / 2)) 14 2

# --- cast ---
$run = [System.Drawing.Bitmap]::FromFile((Join-Path $PackSprites 'RUN.png'))
StandOn (CutCell $run (96 * $RunFrame) 0 96 96 $false) 84 $Surface
StandOn (LoadArt 'Obstacles\obstacle_rock.png') 158 $Surface
StandOn (CutCell (LoadArt 'UI\hp_onigiri.png') 0 0 19 17 $false) 212 ($Surface - 2)
StandOn (CutCell (LoadArt 'Enemies\archer_idle.png') 0 0 64 48 $false) 270 $Surface
StandOn (CutCell (LoadArt 'Enemies\ninja_idle.png') 0 0 64 48 $false) 342 $Surface
DrawAt (CutCell (LoadArt 'Weapons\weapon_arrow.png') 0 0 24 5 $true) 190 ($Surface - 26) 1   # the sheet points right; this one flies left
DrawAt (LoadArt 'Weapons\weapon_shuriken.png') 300 ($Surface - 30) 1

# --- scale up with hard pixel edges and save ---
$out = New-Object System.Drawing.Bitmap(($CanvasW * $OutScale), ($CanvasH * $OutScale), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$outGfx = [System.Drawing.Graphics]::FromImage($out)
$outGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$outGfx.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$outGfx.DrawImage($canvas, (New-Object System.Drawing.Rectangle(0, 0, $out.Width, $out.Height)), 0, 0, $CanvasW, $CanvasH, [System.Drawing.GraphicsUnit]::Pixel)
$path = Join-Path $OutDir 'banner.png'
$out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
"wrote $path ($($out.Width)x$($out.Height)); ground surface row $Surface; run frame $RunFrame"
