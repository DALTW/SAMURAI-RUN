param([string]$Mode = 'preview')
# Title screen sprites for Samurai Run.
#  - title_logo.png : "SAMURAI RUN" in the FREE_Samurai pack logo style (letters cut from Preview.png, N/T drawn to match) + 1px dark outline
#  - start_label.png: "START" in the same letter style
#  - ui_button.png  : 16x16 navy pixel button for 9-slice (border 4px)
# Usage: powershell -File Tools\gen_title_sprites.ps1 preview   (writes to Temp\TitlePreview)
#        powershell -File Tools\gen_title_sprites.ps1 write     (writes into Assets\Art\UI)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Proj = Split-Path -Parent $PSScriptRoot
$PreviewPng = Join-Path $Proj 'Assets\Assets\FREE_Samurai 2D Pixel Art v1.2\Preview.png'
$OutDir = if ($Mode -eq 'write') { Join-Path $Proj 'Assets\Art\UI' } else { Join-Path $Proj 'Temp\TitlePreview' }
New-Item -ItemType Directory -Force $OutDir | Out-Null

$Colors = New-Object System.Collections.Hashtable
foreach ($pair in @(@('o', 'FFEDAB50'), @('e', 'FFE07438'), @('r', 'FF8E251D'), @('k', 'FF1A1932'), @('K', 'FF0E071B'), @('n', 'FF2A2F4E'), @('b', 'FF424C6E'), @('l', 'FF92A1B9'))) {
  $Colors[$pair[0]] = [System.Drawing.Color]::FromArgb([Convert]::ToInt32($pair[1], 16))
}

# --- letters cut from the pack logo (Preview.png, logo at x=40,y=40, drawn at 5x) ---
$src = [System.Drawing.Bitmap]::FromFile($PreviewPng)
$logoMap = @{ 'FFEDAB50' = 'o'; 'FFE07438' = 'e'; 'FF8E251D' = 'r' }
function CutLetter([int]$col0, [int]$width) {
  $rows = @()
  for ($y = 0; $y -lt 14; $y++) {
    $row = ''
    for ($x = 0; $x -lt $width; $x++) {
      $k = $src.GetPixel(40 + ($col0 + $x) * 5, 40 + $y * 5).ToArgb().ToString('X8')
      $row += $(if ($logoMap.ContainsKey($k)) { $logoMap[$k] } else { '.' })
    }
    $rows += $row
  }
  return ,$rows
}
$L = @{}
$L['S'] = CutLetter 0 11
$L['A'] = CutLetter 12 11
$L['M'] = CutLetter 25 11
$L['U'] = CutLetter 37 11
$L['R'] = CutLetter 50 11
$L['I'] = CutLetter 75 3
$src.Dispose()

# --- letters drawn to match (same 3px strokes, e/r shading under faces) ---
$L['N'] = @(
  'ooo.....ooo',
  'ooo.....ooo',
  'oooo....ooo',
  'ooooo...ooo',
  'oooooo..ooo',
  'oooeooo.ooo',
  'oooreoooooo',
  'ooo.reooooo',
  'ooo..reoooo',
  'ooo...reooo',
  'ooo....rooo',
  'eee.....eee',
  'rrr.....rrr',
  'rrr.....rrr')
$L['T'] = @(
  'ooooooooooo',
  'ooooooooooo',
  'ooooooooooo',
  'eeeeoooeeee',
  'rrrrooorrrr',
  '....ooo....',
  '....ooo....',
  '....ooo....',
  '....ooo....',
  '....ooo....',
  '....ooo....',
  '....eee....',
  '....rrr....',
  '....rrr....')
$L[' '] = @('......') * 14

function Compose([string]$text, [int]$gap) {
  $rows = @('') * 14
  $chars = $text.ToCharArray()
  for ($i = 0; $i -lt $chars.Length; $i++) {
    $g = $L[[string]$chars[$i]]
    for ($y = 0; $y -lt 14; $y++) { $rows[$y] += $g[$y]; if ($i -lt $chars.Length - 1 -and $chars[$i] -ne ' ' -and $chars[$i + 1] -ne ' ') { $rows[$y] += ('.' * $gap) } }
  }
  return ,$rows
}
function AddOutline($rows, [char]$outline) {
  $h = $rows.Count; $w = $rows[0].Length
  $grid = New-Object 'char[,]' ($h + 2), ($w + 2)
  for ($y = 0; $y -lt $h + 2; $y++) { for ($x = 0; $x -lt $w + 2; $x++) { $grid[$y, $x] = '.' } }
  for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { $grid[($y + 1), ($x + 1)] = $rows[$y][$x] } }
  $out = New-Object 'char[,]' ($h + 2), ($w + 2)
  for ($y = 0; $y -lt $h + 2; $y++) {
    for ($x = 0; $x -lt $w + 2; $x++) {
      $c = $grid[$y, $x]; $out[$y, $x] = $c
      if ($c -ne '.') { continue }
      foreach ($d in @(@(-1, 0), @(1, 0), @(0, -1), @(0, 1), @(-1, -1), @(1, -1), @(-1, 1), @(1, 1))) {
        $yy = $y + $d[0]; $xx = $x + $d[1]
        if ($yy -ge 0 -and $yy -lt $h + 2 -and $xx -ge 0 -and $xx -lt $w + 2 -and $grid[$yy, $xx] -ne '.') { $out[$y, $x] = $outline; break }
      }
    }
  }
  $res = @()
  for ($y = 0; $y -lt $h + 2; $y++) { $r = ''; for ($x = 0; $x -lt $w + 2; $x++) { $r += $out[$y, $x] }; $res += $r }
  return ,$res
}
function SavePng($rows, [string]$path) {
  $h = $rows.Count; $w = $rows[0].Length
  $bmp = New-Object System.Drawing.Bitmap -ArgumentList $w, $h
  for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { $ch = [string]$rows[$y][$x]; if ($Colors.ContainsKey($ch)) { $bmp.SetPixel($x, $y, $Colors[$ch]) } } }
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  "{0}: {1}x{2}" -f (Split-Path -Leaf $path), $w, $h
}

$title = AddOutline (Compose 'SAMURAI RUN' 1) 'k'
SavePng $title (Join-Path $OutDir 'title_logo.png')
$start = AddOutline (Compose 'START' 1) 'k'
SavePng $start (Join-Path $OutDir 'start_label.png')
$button = @(
  '.kkkkkkkkkkkkkk.',
  'kllllllllllllllk',
  'klbbbbbbbbbbbbbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'klbnnnnnnnnnnnbk',
  'kbnnnnnnnnnnnnnk',
  'kKKKKKKKKKKKKKKk',
  'kKKKKKKKKKKKKKKk',
  '.kkkkkkkkkkkkkk.')
SavePng $button (Join-Path $OutDir 'ui_button.png')
"output: $OutDir"
