$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$W = 64; $H = 48
$Root = "C:\Users\ppong\game4-main\Assets\Art"
$Out  = "C:\Users\ppong\AppData\Local\Temp\claude\C--Users-ppong-game4-main\4a9dcf1e-d411-4ec9-959d-64c5453bdf69\scratchpad"
# 팔레트: FREE_Samurai 팩 색 그대로 + 궁수용 올리브/갈색/짚색 (외곽선 없이 톤으로만 형태를 잡음)
$Pal = New-Object System.Collections.Hashtable
$Colors = New-Object System.Collections.Hashtable
foreach ($pair in @(
  @('k','FF1A1932'), @('K','FF0E071B'), @('n','FF2A2F4E'), @('b','FF424C6E'), @('l','FF92A1B9'), @('w','FFC7CFDD'), @('W','FFFFFFFF'),
  @('s','FFE69C69'), @('S','FFF6CA9F'), @('d','FFBF6F4A'), @('r','FF571C27'), @('R','FF5D2C28'), @('Q','FF8A3A3A'), @('p','FF391F21'),
  @('x','FF131313'), @('m','FF6B4530'), @('M','FF452B1F'), @('h','FFD9B77A'), @('H','FFA8834D'), @('e','FF5E6B3A'), @('E','FF3F4A28'),
  @('g','FF858585'), @('G','FFB4B4B4'))) {
  $Pal[$pair[0]] = $pair[1]
  $Colors[$pair[0]] = [System.Drawing.Color]::FromArgb([Convert]::ToInt32($pair[1], 16))
}

function NewC { New-Object 'char[,]' $H, $W }
function Blit($c, [string]$part, [int]$x, [int]$y) {
  $lines = ($part -replace "`r", "") -split "`n"
  for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    for ($j = 0; $j -lt $line.Length; $j++) {
      $ch = $line[$j]
      if ($ch -eq '.' -or $ch -eq ' ') { continue }
      $px = $x + $j; $py = $y + $i
      if ($px -ge 0 -and $px -lt $W -and $py -ge 0 -and $py -lt $H) { $c[$py, $px] = $ch }
    }
  }
}
function Px($c, [int]$x, [int]$y, [char]$ch) { if ($x -ge 0 -and $x -lt $W -and $y -ge 0 -and $y -lt $H) { $c[$y, $x] = $ch } }
function Line($c, [int]$x0, [int]$y0, [int]$x1, [int]$y1, [char]$ch) {
  $dx = [Math]::Abs($x1 - $x0); $dy = -[Math]::Abs($y1 - $y0)
  $sx = if ($x0 -lt $x1) { 1 } else { -1 }; $sy = if ($y0 -lt $y1) { 1 } else { -1 }
  $err = $dx + $dy
  while ($true) {
    Px $c $x0 $y0 $ch
    if ($x0 -eq $x1 -and $y0 -eq $y1) { break }
    $e2 = 2 * $err
    if ($e2 -ge $dy) { $err += $dy; $x0 += $sx }
    if ($e2 -le $dx) { $err += $dx; $y0 += $sy }
  }
}
function Bbox($c) {
  $x0 = $W; $y0 = $H; $x1 = -1; $y1 = -1
  for ($y = 0; $y -lt $H; $y++) { for ($x = 0; $x -lt $W; $x++) { $ch = $c[$y, $x]; if ($ch -ne [char]0 -and $Colors.ContainsKey([string]$ch)) { if ($x -lt $x0) { $x0 = $x }; if ($x -gt $x1) { $x1 = $x }; if ($y -lt $y0) { $y0 = $y }; if ($y -gt $y1) { $y1 = $y } } } }
  return @($x0, $y0, $x1, $y1)
}
function SaveSheet($frames, [string]$path, [string]$preview, [int]$cw, [int]$chh) {
  $n = $frames.Count
  $bmp = New-Object System.Drawing.Bitmap -ArgumentList ($cw * $n), $chh
  for ($f = 0; $f -lt $n; $f++) {
    $c = $frames[$f]
    for ($y = 0; $y -lt $chh; $y++) { for ($x = 0; $x -lt $cw; $x++) {
      $ch = $c[$y, $x]
      if ($ch -ne [char]0 -and $Colors.ContainsKey([string]$ch)) { $bmp.SetPixel($f * $cw + $x, $y, $Colors[[string]$ch]) }
    } }
  }
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  if ($preview) {
    $s = 3
    $pv = New-Object System.Drawing.Bitmap -ArgumentList ($cw * $n * $s), ($chh * $s)
    $g = [System.Drawing.Graphics]::FromImage($pv); $g.Clear([System.Drawing.Color]::FromArgb(255, 96, 104, 132))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, ($cw * $n * $s), ($chh * $s)), (New-Object System.Drawing.Rectangle 0, 0, ($cw * $n), $chh), [System.Drawing.GraphicsUnit]::Pixel)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 255, 255, 255))
    for ($f = 1; $f -lt $n; $f++) { $g.DrawLine($pen, $f * $cw * $s, 0, $f * $cw * $s, $chh * $s) }
    $g.Dispose(); $pv.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png); $pv.Dispose()
  }
  $bmp.Dispose()
}
function SaveSingle($c, [string]$path, [int]$x0, [int]$y0, [int]$x1, [int]$y1) {
  $bw = $x1 - $x0 + 1; $bh = $y1 - $y0 + 1
  $bmp = New-Object System.Drawing.Bitmap -ArgumentList $bw, $bh
  for ($y = 0; $y -lt $bh; $y++) { for ($x = 0; $x -lt $bw; $x++) { $yy = $y0 + $y; $xx = $x0 + $x; $ch = $c[$yy, $xx]; if ($ch -ne [char]0 -and $Colors.ContainsKey([string]$ch)) { $bmp.SetPixel($x, $y, $Colors[[string]$ch]) } } }
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

# ======================= ARCHER (facing left, light from upper-left, no outlines) =======================
$A_HAT = @'
........hh........
......hhhhH.......
....hhhhhhHH......
..hhhhhhhhHHHH....
hhhhhhhhhhHHHHHH..
hHHHHHHHHHHHHHHHHM
.MMMMMMMMMMMMMMMM.
'@
$A_HEAD = @'
ddsssddd
sxSsxsdd
sssssssd
ddsssddd
.ddsdd..
'@
$A_TORSO = @'
....ewsdE....
...eewwdEE...
..eeewwEEEE..
..eeeewEEEE..
.eeeeeeEEEEE.
.eeeeeeEEEEE.
.eeeeeeEEEEE.
.eeeeeeEEEEE.
.rrrrrrrrRRR.
.KKKKKKKKKKK.
'@
$A_LEGS = @'
mmmmmmMMMMM.
mmmmmmMMMMM.
mmmmmmMMMMM.
mmmmmmMMMMM.
mmmmm.MMMMM.
wlll..llll..
wlll..llll..
llll..llll..
RRp...RRp...
RRp...RRp...
RRpp..RRpp..
pppp..pppp..
'@
$A_KNEEL = @'
mmmmmmMMMMM.
mmmmm.MMMMM.
wlll..llll..
RRpp..RRpp..
pppp..pppp..
'@
$A_BACKARM = @'
eE.
eEE
eEE
eEE
eEE
.EE
.sd
.sd
..d
'@
$A_FRONTARM_IDLE = @'
........eee
......eeeeE
....eeeeEE.
..eeeeEE...
.eeeEE.....
.SsE.......
ssd........
sdd........
.d.........
'@
$A_FRONTARM_RAISED = @'
....eeeeeeeeeee
.SseeeeeeeeeeEE
ssdeeeEEEEEEEE.
.sd............
'@
$A_HAND = @'
sS.
ssd
.d.
'@
$A_BACKARM_FULL = @'
sS...........
ssdeeeee.....
sddeeeeeEE...
...EEEeeeeE..
......EEeeeE.
........EEeE.
.........EEE.
'@
$A_BACKARM_RELEASE = @'
S.S..........
sSs..........
ssdeeeee.....
sddeeeeeEE...
...EEEeeeeE..
......EEeeeE.
........EEeE.
.........EEE.
'@
$BOW = @'
....mM
...mM.
...mM.
..mM..
..mM..
.mM...
.mM...
mM....
mM....
mM....
mM....
mM....
pp....
pp....
pp....
mM....
mM....
mM....
mM....
mM....
.mM...
.mM...
..mM..
..mM..
...mM.
...mM.
....mM
'@
$BOW_FLEX = @'
.....mM
....mM.
....mM.
...mM..
..mM...
..mM...
.mM....
.mM....
mM.....
mM.....
mM.....
mM.....
pp.....
pp.....
pp.....
mM.....
mM.....
mM.....
mM.....
.mM....
.mM....
..mM...
..mM...
...mM..
....mM.
....mM.
.....mM
'@
$ARROW_SPR = @'
.G..................rR
GWmMMMMMMMMMMMMMMMMMrR
.G..................rR
'@
$A_LYING = @'
............................hhhh....
..........................hhhhhhHH..
..........llllwwww........HHHHHHHHM.
.......mmmmmeeeeeerreeeeeedsssssdd..
....mmmmmmmmeeeeeerreeeeeeedssssd...
.RRRRlllllmmmeeeeeKKeeeeEEEEdddd....
RRppppppppppEEEEEEEEEEEEEE..........
pppp................................
'@

function ArcherBody($c, [int]$dx, [int]$dy, [bool]$legs = $true) {
  if ($legs) { Blit $c $A_LEGS 38 36 }
  Blit $c $A_TORSO (37 + $dx) (26 + $dy)
  Blit $c $A_HEAD (39 + $dx) (21 + $dy)
  Blit $c $A_HAT (32 + $dx) (14 + $dy)
}
function ArcherIdle($c, [int]$bob) {
  ArcherBody $c 0 $bob
  Blit $c $A_BACKARM 48 (27 + $bob)
  Blit $c $BOW 26 (20 + $bob)
  Line $c 31 (21 + $bob) 31 (45 + $bob) 'l'
  Blit $c $A_FRONTARM_IDLE 27 (27 + $bob)
}
function ArcherDraw($c, [string]$stage) {
  ArcherBody $c 0 0
  $bowX = 23; $bowY = 16
  if ($stage -eq 'full') { Blit $c $BOW_FLEX $bowX $bowY; $tipX = $bowX + 6 } else { Blit $c $BOW $bowX $bowY; $tipX = $bowX + 5 }
  $tipTop = $bowY + 1; $tipBot = $bowY + 25
  switch ($stage) {
    'nock'    { Line $c $tipX $tipTop $tipX $tipBot 'l'; Blit $c $ARROW_SPR 8 27 }
    'half'    { Line $c $tipX $tipTop 33 29 'l'; Line $c 33 29 $tipX $tipBot 'l'; Blit $c $ARROW_SPR 13 27 }
    'full'    { Line $c $tipX $tipTop 40 28 'l'; Line $c 40 28 $tipX $tipBot 'l'; Blit $c $ARROW_SPR 20 27 }
    'release' { Line $c $tipX $tipTop $tipX $tipBot 'l' }
  }
  Blit $c $A_FRONTARM_RAISED 22 27
  switch ($stage) {
    'nock' { for ($x = 31; $x -le 47; $x++) { Px $c $x 31 'e'; Px $c $x 32 'e'; Px $c $x 33 'E' }; Blit $c $A_HAND 27 28 }
    'half' { for ($x = 37; $x -le 47; $x++) { Px $c $x 31 'e'; Px $c $x 32 'e'; Px $c $x 33 'E' }; Blit $c $A_HAND 33 28 }
    'full' { Blit $c $A_BACKARM_FULL 40 26 }
    'release' { Blit $c $A_BACKARM_RELEASE 43 25 }
  }
}
function ArcherDeath($c, [int]$stage) {
  switch ($stage) {
    0 { ArcherBody $c 2 0; Blit $c $A_BACKARM 50 27; Blit $c $BOW 28 20; Line $c 33 21 33 45 'l'; Blit $c $A_FRONTARM_IDLE 29 27 }
    1 { Blit $c $A_KNEEL 38 43; ArcherBody $c 3 5 $false; Blit $c $A_BACKARM 51 32; Blit $c $A_FRONTARM_IDLE 30 32 }
    2 { Blit $c $A_LYING 22 40 }
    3 { Blit $c $A_LYING 22 40 }
  }
}

# ======================= NINJA (facing left, no outlines) =======================
$N_HEAD = @'
...bbnnn..
..bbnnnnn.
.bbnnnnnnK
.bnnnnnnnK
.sxSsnnnnK
.ssssnnnnK
.nnnnnnnKK
..nnnnnnK.
...nnnnK..
'@
$N_SCARF_A = @'
rrQ....
.rrQQ..
..rrrQQ
....rrr
'@
$N_SCARF_B = @'
rQ.....
.rQQ...
..rrQQ.
...rrrQ
....rrr
'@
$N_TORSO = @'
...bnnn....
..bbnnnnK..
.bbnnnnnKK.
.bbnnnnnKK.
.bnnnnnnKK.
.bnnnnnnKK.
.rrrrrrrrr.
.bnnnnnnKK.
.bnnnnnnKK.
.bnnnnnnKK.
'@
$N_LEGS = @'
.bnnnnnnKK.
.bnnnnnnKK.
.bnnnnnKKK.
.bnnn.nnKK.
.bnnn.nnKK.
.wlll.lllK.
.wlll.lllK.
.llll.llll.
.KKk..KKk..
.KKk..KKk..
KKkk..KKkk.
ppp...pppp.
'@
$N_KNEEL = @'
.bnnnnnnKK.
.bnnn.nnKK.
.wlll.lllK.
KKkk..KKkk.
ppp...pppp.
'@
$N_FRONTARM = @'
bn..
bnn.
bnn.
bnn.
wll.
wll.
ssd.
ssd.
.d..
'@
$N_BACKARM = @'
.nK.
.nKK
.nKK
.nKK
.llK
.llK
.sdd
.sdd
..d.
'@
$N_ARM_WINDUP = @'
.........sS.
........ssd.
.......lsd..
......lld...
.....bnn....
....bnnK....
...bnnK.....
..bnnK......
.bnnK.......
bnnK........
'@
$N_ARM_THROW = @'
.S.bbnnnnnnnnn.
sSsllnnnnnnnnnK
ssdllnnnnnnKKK.
.d.............
'@
$N_ARM_RECOVER = @'
.......bn
.....bbnn
...bbnnK.
.wllnK...
SssK.....
ssd......
.d.......
'@
$STAR_A = @'
..G..
.GGg.
GGKgg
.ggg.
..g..
'@
$STAR_B = @'
G...g
.GGg.
.gKg.
.ggg.
g...g
'@
$N_LYING = @'
............................bbnnn...
..........................bbnnnnnKK.
..........llllwwww........bnnnsxsnKQ
.......nnnnnnnnnnnrrnnnnnnnnnnnnKKrr
....nnnnnnnnnnnnnnrrnnnnnnnnnKKK..rr
.KKKKllllnnnnnnnnnKKnnnnnKKKKKK.....
KKpppppppppKKKKKKKKKKKKKK...........
pppp................................
'@
function NinjaBody($c, [int]$dx, [int]$dy, [bool]$legs = $true, [string]$scarf = 'A') {
  if ($legs) { Blit $c $N_LEGS 38 36 }
  Blit $c $N_TORSO (38 + $dx) (26 + $dy)
  Blit $c $N_HEAD (38 + $dx) (17 + $dy)
  if ($scarf -eq 'A') { Blit $c $N_SCARF_A (47 + $dx) (19 + $dy) } else { Blit $c $N_SCARF_B (47 + $dx) (19 + $dy) }
}
function NinjaIdle($c, [int]$bob, [string]$scarf) {
  NinjaBody $c 0 $bob $true $scarf
  Blit $c $N_BACKARM 46 (27 + $bob)
  Blit $c $N_FRONTARM 36 (27 + $bob)
}
function NinjaAttack($c, [string]$stage) {
  switch ($stage) {
    'wind1'   { NinjaBody $c 1 0 $true 'B'; Blit $c $N_FRONTARM 37 27; Blit $c $N_ARM_WINDUP 45 18; Blit $c $STAR_A 56 16 }
    'wind2'   { NinjaBody $c 1 0 $true 'B'; Blit $c $N_FRONTARM 37 27; Blit $c $N_ARM_WINDUP 46 17; Blit $c $STAR_B 57 15 }
    'throw'   { NinjaBody $c -1 0 $true 'A'; Blit $c $N_BACKARM 45 27; Blit $c $N_ARM_THROW 23 26 }
    'throw2'  { NinjaBody $c -1 0 $true 'B'; Blit $c $N_BACKARM 45 27; Blit $c $N_ARM_THROW 23 26 }
    'recover' { NinjaBody $c 0 0 $true 'A'; Blit $c $N_BACKARM 46 27; Blit $c $N_ARM_RECOVER 30 27 }
  }
}
function NinjaDeath($c, [int]$stage) {
  switch ($stage) {
    0 { NinjaBody $c 2 0 $true 'B'; Blit $c $N_BACKARM 48 27; Blit $c $N_FRONTARM 38 27 }
    1 { Blit $c $N_KNEEL 38 43; NinjaBody $c 3 5 $false 'B'; Blit $c $N_BACKARM 49 32; Blit $c $N_FRONTARM 39 32 }
    2 { Blit $c $N_LYING 22 40 }
    3 { Blit $c $N_LYING 22 40 }
  }
}

# ======================= BUILD FRAMES =======================
$archer = @{}
$L = New-Object System.Collections.ArrayList
foreach ($bob in 0, 0, 1, 1) { $c = NewC; ArcherIdle $c $bob; [void]$L.Add($c) }
$archer['idle'] = $L
$L = New-Object System.Collections.ArrayList
foreach ($st in 'nock', 'half', 'full', 'full', 'release', 'release') { $c = NewC; ArcherDraw $c $st; [void]$L.Add($c) }
foreach ($bob in 0, 0) { $c = NewC; ArcherIdle $c $bob; [void]$L.Add($c) }
$archer['attack'] = $L
$L = New-Object System.Collections.ArrayList
foreach ($st in 0, 1, 2, 3) { $c = NewC; ArcherDeath $c $st; [void]$L.Add($c) }
$archer['death'] = $L

$ninja = @{}
$L = New-Object System.Collections.ArrayList
foreach ($f in @(@(0,'A'), @(0,'B'), @(1,'B'), @(1,'A'))) { $c = NewC; NinjaIdle $c $f[0] $f[1]; [void]$L.Add($c) }
$ninja['idle'] = $L
$L = New-Object System.Collections.ArrayList
foreach ($st in 'wind1', 'wind2', 'wind2', 'throw', 'throw2', 'recover') { $c = NewC; NinjaAttack $c $st; [void]$L.Add($c) }
$c = NewC; NinjaIdle $c 0 'A'; [void]$L.Add($c)
$ninja['attack'] = $L
$L = New-Object System.Collections.ArrayList
foreach ($st in 0, 1, 2, 3) { $c = NewC; NinjaDeath $c $st; [void]$L.Add($c) }
$ninja['death'] = $L

# ======================= WEAPONS (no outlines) =======================
$ARROW = @'
.rR.....................
..rRRMMMMMMMMMMMMMMMMG..
rrrRRmmmmmmmmmmmmmmmmGGW
..rRR................G..
.rR.....................
'@
$SHURIKEN = @'
.....G.....
....GGg....
....GGg....
...GGGgg...
GGGGGGgggg.
GGGGGKggggg
.gggggggggK
...gggggK..
....ggKK...
....gKK....
.....K.....
'@

# ======================= OUTPUT =======================
$mode = if ($args.Count -gt 0) { $args[0] } else { 'preview' }
foreach ($name in 'archer', 'ninja') {
  $set = if ($name -eq 'archer') { $archer } else { $ninja }
  foreach ($clip in 'idle', 'attack', 'death') {
    $frames = $set[$clip]
    $path = if ($mode -eq 'write') { "$Root\Enemies\${name}_$clip.png" } else { "$Out\${name}_$clip.png" }
    SaveSheet $frames $path "$Out\preview_${name}_$clip.png" $W $H
    "$name/$clip : $($frames.Count) frames -> $path"
  }
  $bb = Bbox $set['idle'][0]
  "$name idle0 bbox x[$($bb[0])..$($bb[2])] y[$($bb[1])..$($bb[3])] size $($bb[2]-$bb[0]+1)x$($bb[3]-$bb[1]+1)"
  if ($mode -eq 'write') { SaveSingle $set['idle'][0] "$Root\Enemies\enemy_$name.png" $bb[0] $bb[1] $bb[2] $bb[3] }
}
$wa = New-Object 'char[,]' 5, 24; $lines = ($ARROW -replace "`r","") -split "`n"; for ($i=0;$i -lt 5;$i++){ for($j=0;$j -lt 24;$j++){ $wa[$i,$j] = $lines[$i][$j] } }
$ws = New-Object 'char[,]' 11, 11; $lines = ($SHURIKEN -replace "`r","") -split "`n"; for ($i=0;$i -lt 11;$i++){ for($j=0;$j -lt 11;$j++){ $ws[$i,$j] = $lines[$i][$j] } }
$wl = New-Object System.Collections.ArrayList; [void]$wl.Add($wa)
$pa = if ($mode -eq 'write') { "$Root\Weapons\weapon_arrow.png" } else { "$Out\weapon_arrow.png" }
SaveSheet $wl $pa "$Out\preview_weapon_arrow.png" 24 5
$wl = New-Object System.Collections.ArrayList; [void]$wl.Add($ws)
$ps = if ($mode -eq 'write') { "$Root\Weapons\weapon_shuriken.png" } else { "$Out\weapon_shuriken.png" }
SaveSheet $wl $ps "$Out\preview_weapon_shuriken.png" 11 11
"weapons written ($mode)"
# 사무라이와 나란히 비교용 미리보기 (대기 1프레임씩)
$cmp = New-Object System.Drawing.Bitmap -ArgumentList 200, 48
$g = [System.Drawing.Graphics]::FromImage($cmp); $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
$sam = [System.Drawing.Bitmap]::FromFile("C:\Users\ppong\game4-main\Assets\Assets\FREE_Samurai 2D Pixel Art v1.2\Sprites\IDLE.png")
$g.DrawImage($sam, (New-Object System.Drawing.Rectangle 4, 0, 40, 48), (New-Object System.Drawing.Rectangle 28, 40, 40, 48), [System.Drawing.GraphicsUnit]::Pixel)
$sam.Dispose(); $g.Dispose()
$cmpFrames = New-Object System.Collections.ArrayList
$cc = NewC; for ($y=0;$y -lt 48;$y++){ for($x=0;$x -lt 64;$x++){ $p = $cmp.GetPixel($x,$y); if ($p.A -gt 0) { $k = $p.ToArgb().ToString('X8'); foreach ($kk in $Pal.Keys) { if ($Pal[$kk] -eq $k) { $cc[$y,$x] = [char]$kk; break } } } } }
[void]$cmpFrames.Add($cc); [void]$cmpFrames.Add($archer['idle'][0]); [void]$cmpFrames.Add($ninja['idle'][0])
$cmp.Dispose()
SaveSheet $cmpFrames "$Out\compare_1x.png" "$Out\preview_compare.png" $W $H
"compare preview written"
