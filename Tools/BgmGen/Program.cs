// Samurai Run background music generator (Japanese period-drama style, seamless loop).
// Usage:  dotnet run -c Release --project Tools/BgmGen -- <output.wav> [--spectrogram <sheet.png>]
//
// Scale: Miyako-bushi (In scale) on D = D Eb G A Bb.   136 BPM, 4/4, 40 bars (about 70.6 s), 44.1 kHz 16-bit stereo.
// Instruments (all synthesized, no samples):
//   shakuhachi - breathy bamboo flute lead with scoops, legato glides, breath gaps and late vibrato
//   koto       - plucked zither ostinato and tremolo melody (re-plucking a string damps its previous ring)
//   shamisen   - twangy buzzing bass plucks with a skin "pop"
//   odaiko     - big taiko drum, shime-daiko - tight high drum, hyoshigi - wooden clappers
// Form: Intro(4) A(8) A'(8) B(8, high climax) Bridge(8, koto tremolo melody) Turnaround(4, taiko roll) -> back to Intro.
// Everything that rings past the end of bar 40 is folded back onto the start, so the file loops without a seam.

const int SR = 44100;
const double BPM = 136;
const int Bars = 40;
double beatSec = 60.0 / BPM;
string outPath = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "bgm_samurai.wav";
var rng = new Random(1603);
double Noise() => rng.NextDouble() * 2 - 1;
double Jit(double amount) => (rng.NextDouble() * 2 - 1) * amount;

int loopLen = (int)Math.Round(Bars * 4 * beatSec * SR);
double loopSec = loopLen / (double)SR;
int fullLen = loopLen + 5 * SR;                 // room for ringing tails (folded back onto the start later)
var mixL = new float[fullLen]; var mixR = new float[fullLen]; var send = new float[fullLen];
var stems = new Dictionary<string, float[]>();  // mono copy per instrument, only for the level report

double Sec(int bar, double beat) => ((bar - 1) * 4 + beat) * beatSec;   // bar is 1-based
double Midi(double n) => 440 * Math.Pow(2, (n - 69) / 12.0);

// ----------------------------------------------------------------------------------------------
// Harmony: root of each bar, as koto pitch
const int D = 62, Eb = 63, G = 55, A = 57, Bb = 58;
var root = new int[Bars + 1];
void Fill(int fromBar, params int[] roots) { for (int i = 0; i < roots.Length; i++) root[fromBar + i] = roots[i]; }
Fill(1, D, D, D, D);                                  // Intro
Fill(5, D, D, G, G, Eb, Eb, D, A);                    // A
Fill(13, D, D, G, G, Eb, Eb, D, A);                   // A'
Fill(21, G, G, D, D, Eb, Bb, A, A);                   // B
Fill(29, Bb, Bb, G, G, Eb, Eb, A, A);                 // Bridge
Fill(37, Eb, Eb, A, A);                               // Turnaround
string Section(int bar) => bar <= 4 ? "intro" : bar <= 20 ? "A" : bar <= 28 ? "B" : bar <= 36 ? "bridge" : "turn";

int[] scalePc = { 2, 3, 7, 9, 10 };                   // D Eb G A Bb
int NextUp(int n) { for (int m = n + 1; m < n + 13; m++) if (scalePc.Contains(m % 12)) return m; return n + 12; }
int Partner(int r) => r == A || r == Bb ? r + 5 : r + 7; // the fifth when it is in the scale, otherwise the fourth

// ----------------------------------------------------------------------------------------------
// Koto: ostinato on the chord (tones 0 = root, 1 = fifth/fourth, 2 = octave, 3 = next scale tone above the octave)
int[] pat8 = { 0, 1, 2, 1, 3, 1, 2, 1 };
int[] pat16 = { 0, 1, 2, 1, 3, 2, 1, 2, 0, 1, 2, 3, 2, 1, 3, 2 };
var kotoEvents = new List<(double start, int midi, double vel, double bend)>();
for (int bar = 1; bar <= Bars; bar++)
{
    int r = root[bar];
    int[] tones = { r, Partner(r), r + 12, NextUp(r + 12) };
    string sec = Section(bar);
    if (sec == "bridge") { kotoEvents.Add((Sec(bar, 0), r - 12, 0.75, 0)); continue; }   // low root under the tremolo melody

    bool sixteenths = sec == "B" || (sec == "turn" && bar >= 39);
    int[] pat = sixteenths ? pat16 : pat8;
    for (int s = 0; s < pat.Length; s++)
    {
        if (sec == "intro" && bar <= 2 && s % 2 == 1) continue;    // the intro opens with quarter notes
        int note = tones[pat[s]];
        double vel = sixteenths
            ? (s == 0 ? 1.0 : s == 8 ? 0.85 : s % 4 == 0 ? 0.72 : 0.55)
            : (s == 0 ? 1.0 : s == 4 ? 0.85 : 0.62);
        if (sec == "turn") vel *= 0.7 + 0.3 * ((bar - 37) * 4 + s * 4.0 / pat.Length) / 16.0;
        double bend = 0;                                             // oshide: press the string up to the next scale tone
        if (sec == "A" && bar % 2 == 0 && pat[s] == 3 && NextUp(note) - note <= 2) bend = NextUp(note) - note;
        kotoEvents.Add((Sec(bar, s * 4.0 / pat.Length) + Jit(0.004), note, 0.55 * (vel + Jit(0.05)), bend));
    }
}
// Bridge melody as koto tremolo (16th notes, two pitches per bar)
var tremolo = new (int bar, int n1, int n2)[]
{
    (29, 74, 70), (30, 69, 70), (31, 67, 69), (32, 70, 74), (33, 75, 74), (34, 70, 67), (35, 69, 70), (36, 69, 69),
};
foreach (var (bar, n1, n2) in tremolo)
    for (int half = 0; half < 2; half++)
        for (int k = 0; k < 8; k++)
        {
            double swell = 0.55 + 0.45 * Math.Sin(Math.PI * (k + 0.5) / 8);
            kotoEvents.Add((Sec(bar, half * 2 + k * 0.25) + Jit(0.003), half == 0 ? n1 : n2, 0.5 * swell, 0));
        }
var kotoStarts = kotoEvents.GroupBy(e => e.midi).ToDictionary(g => g.Key, g => g.Select(e => e.start).OrderBy(x => x).ToArray());
foreach (var e in kotoEvents)
{
    var starts = kotoStarts[e.midi];
    double next = starts.FirstOrDefault(x => x > e.start + 1e-4, starts[0] + loopSec);  // same string plucked again
    Koto(e.start, e.midi, e.vel, e.bend, next - e.start);
}

// ----------------------------------------------------------------------------------------------
// Shamisen bass (8th-note grid, one string at a time)
var shamiEvents = new List<(double start, int midi, double vel)>();
for (int bar = 1; bar <= Bars; bar++)
{
    int r = root[bar] - 12, f = Partner(root[bar]) - 12;
    (int step, int note, double vel)[] hits = Section(bar) switch
    {
        "intro" => bar <= 2 ? new[] { (0, r, 1.0) } : new[] { (0, r, 1.0), (3, r, 0.7), (6, r, 0.8) },
        "A" => new[] { (0, r, 1.0), (2, r, 0.7), (3, r + 12, 0.75), (5, f, 0.8), (6, r, 0.85) },
        "B" => new[] { (0, r, 1.0), (1, r, 0.6), (2, r + 12, 0.8), (3, r, 0.65), (4, f, 0.85), (5, r, 0.6), (6, r + 12, 0.8), (7, f, 0.7) },
        "bridge" => new[] { (0, r, 0.9), (4, f, 0.7) },
        _ => Enumerable.Range(0, 8).Select(s => (s, r, 0.55 + 0.45 * ((bar - 37) * 8 + s) / 32.0)).ToArray(),
    };
    foreach (var (step, note, vel) in hits) shamiEvents.Add((Sec(bar, step * 0.5) + Jit(0.004), note, vel + Jit(0.04)));
}
shamiEvents.Sort((a, b) => a.start.CompareTo(b.start));
for (int i = 0; i < shamiEvents.Count; i++)
{
    double next = i + 1 < shamiEvents.Count ? shamiEvents[i + 1].start : shamiEvents[0].start + loopSec;
    Shamisen(shamiEvents[i].start, shamiEvents[i].midi, shamiEvents[i].vel, next - shamiEvents[i].start);
}

// ----------------------------------------------------------------------------------------------
// Drums (16th-note grid)
for (int bar = 1; bar <= Bars; bar++)
{
    string sec = Section(bar);
    int[] big;
    switch (sec)
    {
        case "intro": big = bar < 4 ? new[] { 0, 8 } : new[] { 0, 8, 12, 13, 14, 15 }; break;
        case "A": big = bar % 4 == 0 ? new[] { 0, 6, 8, 12, 13, 14 } : new[] { 0, 6, 8, 14 }; break;
        case "B": big = bar % 2 == 0 ? new[] { 0, 3, 6, 8, 11, 14, 15 } : new[] { 0, 3, 6, 8, 11, 14 }; break;
        case "bridge": big = bar < 36 ? new[] { 0 } : new[] { 0, 8 }; break;
        default: big = bar == 37 ? new[] { 0, 4, 8, 12 } : bar == 38 ? new[] { 0, 2, 4, 6, 8, 10, 12, 14 } : Enumerable.Range(0, 16).ToArray(); break;
    }
    foreach (int s in big)
    {
        double vel = s == 0 ? 1.0 : s == 8 ? 0.85 : 0.68;
        if (sec == "intro" && s >= 12) vel = 0.5 + 0.12 * (s - 12);
        if (sec == "bridge") vel *= 0.7;
        if (sec == "turn" && bar >= 39) vel = 0.45 + 0.55 * ((bar - 39) * 16 + s) / 31.0;   // the roll swells into bar 1
        Odaiko(Sec(bar, s * 0.25) + Jit(0.003), vel + Jit(0.05));
    }

    int[] small; double smallVel = 0.5;
    switch (sec)
    {
        case "intro": small = bar < 3 ? new[] { 4, 12 } : new[] { 0, 2, 4, 6, 8, 10, 12, 14 }; break;
        case "A": small = new[] { 0, 2, 4, 6, 8, 10, 12, 14 }; break;
        case "B": small = Enumerable.Range(0, 16).ToArray(); break;
        case "bridge": small = new[] { 4, 12 }; smallVel = 0.35; break;
        default: small = Enumerable.Range(0, 16).ToArray(); break;
    }
    foreach (int s in small)
    {
        double vel = smallVel * (s == 4 || s == 12 ? 1.6 : s % 2 == 0 ? 1.0 : 0.6);
        if (sec == "turn") vel *= 0.6 + 0.6 * ((bar - 37) * 16 + s) / 63.0;
        Shime(Sec(bar, s * 0.25) + Jit(0.003), vel + Jit(0.04));
    }

    // Wooden clappers open each section and mark the phrase ends
    if (bar == 1 || bar == 5 || bar == 13 || bar == 21 || bar == 29 || bar == 37) Hyoshigi(Sec(bar, 0), 1.0);
    if (bar == 12 || bar == 20 || bar == 28) { Hyoshigi(Sec(bar, 3.5), 0.8); Hyoshigi(Sec(bar, 3.75), 1.0); }
}

// ----------------------------------------------------------------------------------------------
// Shakuhachi melody: (bar, beat, length in beats, midi). Shortened notes leave room to breathe.
var melody = new List<(int bar, double beat, double len, int n)>
{
    // Intro call
    (3, 2, 2, 69), (4, 0, 3, 74),
    // A
    (5, 0, 1.5, 69), (5, 1.5, 0.5, 67), (5, 2, 1, 69), (5, 3, 1, 74),
    (6, 0, 1, 75), (6, 1, 1, 74), (6, 2, 1.6, 69),
    (7, 0, 1.5, 67), (7, 1.5, 0.5, 69), (7, 2, 1, 70), (7, 3, 1, 69),
    (8, 0, 2.6, 67), (8, 3, 1, 62),
    (9, 0, 1, 63), (9, 1, 1, 67), (9, 2, 1.5, 70), (9, 3.5, 0.5, 69),
    (10, 0, 2, 67), (10, 2, 1.6, 63),
    (11, 0, 1, 62), (11, 1, 0.5, 63), (11, 1.5, 0.5, 67), (11, 2, 2, 70),
    (12, 0, 3.5, 69),
    // A'
    (13, 0, 1.5, 69), (13, 1.5, 0.5, 70), (13, 2, 1, 74), (13, 3, 1, 75),
    (14, 0, 1, 74), (14, 1, 1, 70), (14, 2, 1.6, 69),
    (15, 0, 1, 67), (15, 1, 1, 69), (15, 2, 1, 70), (15, 3, 1, 74),
    (16, 0, 2, 75), (16, 2, 1.6, 74),
    (17, 0, 1.5, 70), (17, 1.5, 0.5, 69), (17, 2, 1, 67), (17, 3, 1, 63),
    (18, 0, 2, 67), (18, 2, 1.6, 69),
    (19, 0, 1, 74), (19, 1, 0.5, 70), (19, 1.5, 0.5, 69), (19, 2, 1, 67), (19, 3, 1, 63),
    (20, 0, 3.5, 62),
    // B (high register, climax on D6 in bar 24)
    (21, 0, 1, 74), (21, 1, 1, 75), (21, 2, 2, 79),
    (22, 0, 1.5, 81), (22, 1.5, 0.5, 79), (22, 2, 1, 75), (22, 3, 0.6, 74),
    (23, 0, 2, 81), (23, 2, 1, 82), (23, 3, 1, 81),
    (24, 0, 2.6, 86), (24, 3, 1, 81),
    (25, 0, 1, 82), (25, 1, 1, 81), (25, 2, 1, 79), (25, 3, 1, 75),
    (26, 0, 1.5, 74), (26, 1.5, 0.5, 75), (26, 2, 1.6, 79),
    (27, 0, 1, 81), (27, 1, 1, 79), (27, 2, 1, 75), (27, 3, 1, 74),
    (28, 0, 3.5, 69),
    // Bridge: long low tones over the koto tremolo
    (29, 0, 7.5, 70), (31, 0, 7.5, 67), (33, 0, 7.5, 75), (35, 0, 7.5, 69),
    // Turnaround
    (37, 0, 7.5, 75), (39, 0, 3.5, 74), (40, 0, 3.5, 69),
};
RenderShakuhachi(melody);

// ----------------------------------------------------------------------------------------------
// Reverb, fold the tail onto the start (seamless loop), master bus
const float ReverbReturn = 0.32f;
var (revL, revR) = StereoReverb(send, 0.86, 0.32);
var revStem = new float[fullLen];
for (int i = 0; i < fullLen; i++)
{
    mixL[i] += ReverbReturn * revL[i]; mixR[i] += ReverbReturn * revR[i];
    revStem[i] = ReverbReturn * 0.5f * (revL[i] + revR[i]) * 1.414f;
}
stems["reverb"] = revStem;
var outL = new float[loopLen]; var outR = new float[loopLen];
for (int i = 0; i < fullLen; i++) { outL[i % loopLen] += mixL[i]; outR[i % loopLen] += mixR[i]; }
var dryMono = new float[loopLen];
for (int i = 0; i < loopLen; i++) dryMono[i] = 0.5f * (outL[i] + outR[i]) * 1.414f;
HighPass(outL, 30); HighPass(outR, 30);
Normalize(outL, outR, 0.9);
Compress(outL, outR, thresholdDb: -10, ratio: 2, attackMs: 10, releaseMs: 180);
Normalize(outL, outR, 0.891);   // -1 dBFS
WriteStereoWav(outPath, outL, outR);

// ----------------------------------------------------------------------------------------------
// Report (I cannot listen, so levels, balance and the loop seam are checked by numbers and a spectrogram)
var sections = new[] { ("intro", 1, 4), ("A", 5, 12), ("A'", 13, 20), ("B", 21, 28), ("bridge", 29, 36), ("turn", 37, 40) };
(int, int) Range(int b0, int b1) => ((int)(Sec(b0, 0) * SR), Math.Min(loopLen, (int)(Sec(b1 + 1, 0) * SR)));
Console.WriteLine($"wrote {outPath}: {loopSec:0.00} s loop, {Bars} bars at {BPM} BPM, stereo 44.1 kHz");
Console.WriteLine($"master: peak {Db(Peak(outL, outR)):0.0} dBFS, rms {Db(Rms(outL, outR, 0, loopLen)):0.0} dBFS");
foreach (var (name, b0, b1) in sections)
{
    var (s0, s1) = Range(b0, b1);
    Console.WriteLine($"  {name,-7} bars {b0,2}-{b1,2}: rms {Db(Rms(outL, outR, s0, s1)):0.0} dBFS");
}
var stemOrder = new[] { "flute", "koto", "shamisen", "odaiko", "shime", "hyoshigi", "reverb" };
foreach (var weighted in new[] { false, true })
{
    Console.WriteLine(weighted ? "stem levels above 150 Hz (closer to how loud they feel), dB relative to the dry mix:" : "stem levels (plain rms), dB relative to the dry mix:");
    Console.WriteLine($"  {"",-7}" + string.Concat(stemOrder.Select(n => $"{n,10}")));
    var mixW = weighted ? HighPassed(dryMono, 150) : dryMono;
    var stemW = stemOrder.ToDictionary(n => n, n => weighted ? HighPassed(stems[n], 150) : stems[n]);
    foreach (var (name, b0, b1) in sections)
    {
        var (s0, s1) = Range(b0, b1);
        double mixRms = MonoRms(mixW, s0, s1);
        Console.WriteLine($"  {name,-7}" + string.Concat(stemOrder.Select(n => $"{Db(MonoRms(stemW[n], s0, s1) / mixRms),10:0.0}")));
    }
}
Console.WriteLine($"loop seam step: L {Math.Abs(outL[0] - outL[loopLen - 1]):0.0000}, R {Math.Abs(outR[0] - outR[loopLen - 1]):0.0000} (typical step {TypicalStep(outL):0.0000}, largest step {LargestStep(outL):0.0000})");
int si = Array.IndexOf(args, "--spectrogram");
if (si >= 0 && si + 1 < args.Length) SpectrogramSheet(args[si + 1], outL, outR);
return;

// ======================= instruments =======================

void Koto(double startSec, int midi, double vel, double bendSemis, double untilNext)
{
    double f0 = Midi(midi);
    double tau1 = Math.Clamp(0.75 * Math.Sqrt(220 / f0), 0.3, 1.0);
    double natural = Math.Min(2.0, tau1 * 5);
    bool cut = untilNext + 0.03 < natural;
    int len = (int)((cut ? untilNext + 0.03 : natural) * SR);
    var buf = new float[len];
    var ph = new double[7]; var amp = new double[7]; var decay = new double[7];
    for (int k = 1; k <= 7; k++) { amp[k - 1] = 1 / Math.Pow(k, 1.25); decay[k - 1] = Math.Exp(-1 / (tau1 / Math.Pow(k, 0.6) * SR)); }
    var pick = new Biquad(); pick.BandPass(3000, 1.0);
    for (int i = 0; i < len; i++)
    {
        double t = i / (double)SR;
        double f = bendSemis == 0 ? f0 : f0 * Math.Pow(2, bendSemis * Smooth((t - 0.14) / 0.09) / 12);
        double s = 0;
        for (int k = 0; k < 7; k++)
        {
            ph[k] += 2 * Math.PI * (k + 1) * f * (1 + 0.0004 * (k + 1) * (k + 1)) / SR;
            s += Math.Sin(ph[k]) * amp[k];
            amp[k] *= decay[k];
        }
        s += 0.35 * pick.Run(Noise()) * Math.Exp(-t / 0.008);
        buf[i] = (float)(s * Math.Min(1, t / 0.002));
    }
    FadeEnd(buf, cut ? 0.03 : 0.25);
    Place(buf, startSec, 0.42 * vel, -0.35, 0.25, "koto");
}

void Shamisen(double startSec, int midi, double vel, double untilNext)
{
    double f0 = Midi(midi);
    bool cut = untilNext + 0.02 < 0.9;
    int len = (int)((cut ? untilNext + 0.02 : 0.9) * SR);
    var buf = new float[len];
    var ph = new double[10];
    var buzz = new Biquad(); buzz.BandPass(2200, 1.5);
    var click = new Biquad(); click.HighPass(3000, 0.7);
    var skin = new Biquad(); skin.BandPass(420, 1.2);
    for (int i = 0; i < len; i++)
    {
        double t = i / (double)SR, s = 0;
        for (int k = 1; k <= 10; k++)
        {
            ph[k - 1] += 2 * Math.PI * k * f0 / SR;
            s += Math.Sin(ph[k - 1]) / Math.Pow(k, 0.9) * Math.Exp(-t / (0.38 / Math.Pow(k, 0.4)));
        }
        s += 0.25 * buzz.Run(Noise()) * Math.Exp(-t / 0.25) * (1 + 0.5 * Math.Sin(ph[0]));   // sawari buzz
        s += 0.8 * click.Run(Noise()) * Math.Exp(-t / 0.004);                                  // plectrum
        s += 1.2 * skin.Run(Noise()) * Math.Exp(-t / 0.015);                                   // plectrum hitting the skin
        buf[i] = (float)(Math.Tanh(1.5 * s) / Math.Tanh(1.5) * Math.Min(1, t / 0.001));
    }
    FadeEnd(buf, cut ? 0.02 : 0.1);
    Place(buf, startSec, 0.23 * vel, 0.35, 0.15, "shamisen");
}

void Odaiko(double startSec, double vel)
{
    int len = (int)(1.6 * SR);
    var buf = new float[len];
    var lp = new Biquad(); lp.LowPass(400, 0.7); var slap = new Biquad(); slap.BandPass(1200, 1.2);
    double ph = 0, ph2 = 0;
    for (int i = 0; i < len; i++)
    {
        double t = i / (double)SR;
        double f = 58 + 50 * Math.Exp(-t / 0.06);
        ph += 2 * Math.PI * f / SR; ph2 += 2 * Math.PI * 1.58 * f / SR;
        double s = Math.Sin(ph) * Math.Exp(-t / 0.38) + 0.5 * Math.Sin(ph2) * Math.Exp(-t / 0.12)
                 + 0.5 * lp.Run(Noise()) * Math.Exp(-t / 0.05) + 0.45 * slap.Run(Noise()) * Math.Exp(-t / 0.01);
        buf[i] = (float)(Math.Tanh(1.3 * s) * Math.Min(1, t / 0.0015));
    }
    FadeEnd(buf, 0.2);
    Place(buf, startSec, 0.6 * Math.Clamp(vel, 0, 1.2), 0, 0.35, "odaiko");
}

void Shime(double startSec, double vel)
{
    int len = (int)(0.3 * SR);
    var buf = new float[len];
    var crack = new Biquad(); crack.BandPass(2600, 1.2);
    double ph = 0;
    for (int i = 0; i < len; i++)
    {
        double t = i / (double)SR;
        ph += 2 * Math.PI * (300 + 140 * Math.Exp(-t / 0.015)) / SR;
        buf[i] = (float)(Math.Sin(ph) * Math.Exp(-t / 0.07) + 0.8 * crack.Run(Noise()) * Math.Exp(-t / 0.012));
    }
    FadeEnd(buf, 0.05);
    Place(buf, startSec, 0.42 * Math.Clamp(vel, 0, 1.2), -0.2, 0.15, "shime");
}

void Hyoshigi(double startSec, double vel)
{
    int len = (int)(0.15 * SR);
    var buf = new float[len];
    var click = new Biquad(); click.HighPass(4000, 0.7);
    for (int i = 0; i < len; i++)
    {
        double t = i / (double)SR;
        buf[i] = (float)(Math.Sin(2 * Math.PI * 1650 * t) * Math.Exp(-t / 0.03) + 0.6 * Math.Sin(2 * Math.PI * 3870 * t) * Math.Exp(-t / 0.012)
                         + 0.5 * click.Run(Noise()) * Math.Exp(-t / 0.002));
    }
    FadeEnd(buf, 0.03);
    Place(buf, startSec, 0.35 * vel, 0.25, 0.3, "hyoshigi");
}

// Breathy bamboo flute. Notes that touch each other are one legato phrase (the pitch glides between them).
void RenderShakuhachi(List<(int bar, double beat, double len, int n)> notes)
{
    var sorted = notes.Select(x => (start: Sec(x.bar, x.beat), dur: x.len * beatSec, f: Midi(x.n))).OrderBy(x => x.start).ToList();
    int idx = 0;
    while (idx < sorted.Count)
    {
        int end = idx;
        while (end + 1 < sorted.Count && sorted[end + 1].start - (sorted[end].start + sorted[end].dur) < 0.02) end++;
        var phrase = sorted.GetRange(idx, end - idx + 1);
        idx = end + 1;

        double p0 = phrase[0].start, p1 = phrase[^1].start + phrase[^1].dur, release = 0.14;
        int len = (int)((p1 - p0 + release) * SR);
        var buf = new float[len];
        var breathBp = new Biquad(); var air = new Biquad(); air.HighPass(2500, 0.7);
        double ph1 = 0, ph2 = 0, ph3 = 0, ph4 = 0;
        int cur = 0;
        for (int i = 0; i < len; i++)
        {
            double t = p0 + i / (double)SR;
            while (cur + 1 < phrase.Count && t >= phrase[cur + 1].start) cur++;
            var note = phrase[cur];
            double tn = t - note.start;
            double fPrev = cur == 0 ? note.f * Math.Pow(2, -1.0 / 12) : phrase[cur - 1].f;   // scoop up into the first note
            double f = fPrev + (note.f - fPrev) * Smooth(tn / (cur == 0 ? 0.08 : 0.05));
            double vibCents = note.dur > 0.6 ? Math.Clamp((tn - 0.35) * 30, 0, 18) : 0;        // grows on long notes
            f *= Math.Pow(2, vibCents * Math.Sin(2 * Math.PI * 5.2 * tn) / 1200);
            ph1 += 2 * Math.PI * f / SR; ph2 += 4 * Math.PI * f / SR; ph3 += 6 * Math.PI * f / SR; ph4 += 8 * Math.PI * f / SR;

            double attack = cur == 0 ? Smooth(tn / 0.07) : 0.82 + 0.18 * Smooth(tn / 0.05);      // small dip between legato notes
            double swell = note.dur > 1.0 ? 0.88 + 0.12 * Math.Sin(Math.PI * Math.Clamp(tn / note.dur, 0, 1)) : 1;
            double rel = t > p1 ? Math.Max(0, 1 - (t - p1) / release) : 1;
            double amp = attack * swell * rel;

            breathBp.BandPass(f, 2.5);
            double puff = 1 + 2.0 * Math.Exp(-tn / 0.05);
            double tone = Math.Sin(ph1) + 0.25 * Math.Sin(ph2) + 0.1 * Math.Sin(ph3) + 0.03 * Math.Sin(ph4);
            double breath = (1.2 * breathBp.Run(Noise()) + 0.08 * air.Run(Noise())) * puff;
            buf[i] = (float)((0.8 * tone + breath) * amp);
        }
        Place(buf, p0, 0.42, 0.1, 0.45, "flute");
    }
}

// ======================= mixing helpers =======================

void Place(float[] mono, double startSec, double gain, double pan, double sendAmt, string stem)
{
    int start = (int)Math.Round(startSec * SR);
    if (start < 0) start += loopLen;                 // a note nudged before bar 1 belongs to the end of the loop
    if (!stems.TryGetValue(stem, out var st)) stems[stem] = st = new float[fullLen];
    double a = (pan + 1) * Math.PI / 4;              // equal-power pan
    float gl = (float)(gain * Math.Cos(a)), gr = (float)(gain * Math.Sin(a)), gs = (float)(gain * sendAmt), gm = (float)gain;
    for (int i = 0; i < mono.Length; i++)
    {
        int j = start + i; if (j >= fullLen) break;
        float v = mono[i];
        mixL[j] += v * gl; mixR[j] += v * gr; send[j] += v * gs; st[j] += v * gm;
    }
}

(float[], float[]) StereoReverb(float[] input, double feedback, double damp)
{
    int n = input.Length;
    var l = new float[n]; var r = new float[n];
    int[] combs = { 1557, 1617, 1491, 1422 }, aps = { 556, 441 };
    for (int ch = 0; ch < 2; ch++)
    {
        int spread = ch == 0 ? 0 : 23;
        var lines = combs.Select(c => new double[c + spread]).ToArray(); var idx = new int[4]; var store = new double[4];
        var apl = aps.Select(c => new double[c + spread]).ToArray(); var aidx = new int[2];
        var outp = ch == 0 ? l : r;
        for (int i = 0; i < n; i++)
        {
            double x = input[i], wet = 0;
            for (int c = 0; c < 4; c++)
            {
                double y = lines[c][idx[c]];
                store[c] = y * (1 - damp) + store[c] * damp;
                lines[c][idx[c]] = x * 0.2 + store[c] * feedback;
                idx[c] = (idx[c] + 1) % lines[c].Length;
                wet += y;
            }
            for (int k = 0; k < 2; k++)
            {
                double b = apl[k][aidx[k]];
                double y = -wet + b;
                apl[k][aidx[k]] = wet + b * 0.5;
                aidx[k] = (aidx[k] + 1) % apl[k].Length;
                wet = y;
            }
            outp[i] = (float)wet;
        }
    }
    return (l, r);
}

// One-pole DC/rumble filter; the first pass only warms the filter up on the loop so the seam stays continuous.
void HighPass(float[] b, double fc)
{
    double rc = 1 - 2 * Math.PI * fc / SR, prevX = 0, prevY = 0;
    for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < b.Length; i++)
        {
            double x = b[i], y = x - prevX + rc * prevY;
            prevX = x; prevY = y;
            if (pass == 1) b[i] = (float)y;
        }
}

// Stereo-linked compressor; the envelope is primed on the loop first so the seam stays smooth.
void Compress(float[] l, float[] r, double thresholdDb, double ratio, double attackMs, double releaseMs)
{
    double att = Math.Exp(-1 / (attackMs * 0.001 * SR)), rel = Math.Exp(-1 / (releaseMs * 0.001 * SR)), env = 0;
    double grSum = 0, grMax = 0; int grOver3 = 0;
    for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < l.Length; i++)
        {
            double level = Math.Max(Math.Abs(l[i]), Math.Abs(r[i]));
            env = level > env ? att * env + (1 - att) * level : rel * env + (1 - rel) * level;
            if (pass == 0) continue;
            double over = Db(env) - thresholdDb;
            double gr = over > 0 ? over * (1 - 1 / ratio) : 0;
            grSum += gr; grMax = Math.Max(grMax, gr); if (gr > 3) grOver3++;
            double gain = Math.Pow(10, -gr / 20);
            l[i] = (float)(l[i] * gain); r[i] = (float)(r[i] * gain);
        }
    Console.WriteLine($"compressor: average gain reduction {grSum / l.Length:0.0} dB, max {grMax:0.0} dB, more than 3 dB for {100.0 * grOver3 / l.Length:0}% of the time");
}

void Normalize(float[] l, float[] r, double target)
{
    double p = Peak(l, r); if (p <= 0) return;
    float g = (float)(target / p);
    for (int i = 0; i < l.Length; i++) { l[i] *= g; r[i] *= g; }
}

float[] HighPassed(float[] src, double fc)
{
    var hp = new Biquad(); hp.HighPass(fc, 0.707);
    var o = new float[src.Length];
    for (int i = 0; i < src.Length; i++) o[i] = (float)hp.Run(src[i]);
    return o;
}

double Peak(float[] l, float[] r) { double p = 0; for (int i = 0; i < l.Length; i++) p = Math.Max(p, Math.Max(Math.Abs(l[i]), Math.Abs(r[i]))); return p; }
double Rms(float[] l, float[] r, int s0, int s1) { double s = 0; for (int i = s0; i < s1; i++) s += 0.5 * (l[i] * l[i] + r[i] * r[i]); return Math.Sqrt(s / Math.Max(1, s1 - s0)); }
double MonoRms(float[] b, int s0, int s1) { double s = 0; for (int i = s0; i < s1; i++) s += b[i] * b[i]; return Math.Sqrt(s / Math.Max(1, s1 - s0)); }
double TypicalStep(float[] b) { double s = 0; for (int i = 1; i < b.Length; i++) s += Math.Abs(b[i] - b[i - 1]); return s / (b.Length - 1); }
double LargestStep(float[] b) { double m = 0; for (int i = 1; i < b.Length; i++) m = Math.Max(m, Math.Abs(b[i] - b[i - 1])); return m; }
double Db(double v) => 20 * Math.Log10(Math.Max(1e-9, v));
double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
void FadeEnd(float[] b, double seconds) { int n = Math.Min(b.Length, (int)(seconds * SR)); for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= (float)Smooth(i / (double)n); }

void WriteStereoWav(string path, float[] l, float[] r)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var w = new BinaryWriter(File.Create(path));
    int dataBytes = l.Length * 4;
    w.Write("RIFF"u8.ToArray()); w.Write(36 + dataBytes); w.Write("WAVE"u8.ToArray());
    w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(SR); w.Write(SR * 4); w.Write((short)4); w.Write((short)16);
    w.Write("data"u8.ToArray()); w.Write(dataBytes);
    for (int i = 0; i < l.Length; i++)
    {
        w.Write((short)Math.Round(Math.Clamp(l[i], -1f, 1f) * 32767));
        w.Write((short)Math.Round(Math.Clamp(r[i], -1f, 1f) * 32767));
    }
}

// Spectrogram sheet (PNG): one strip per 10 bars, each with bar ticks (tall yellow ticks = section starts),
// a loudness bar graph and a 60 Hz - 10 kHz log-frequency spectrogram, 50 ms per column.
void SpectrogramSheet(string path, float[] l, float[] r)
{
    const int rows = 128, win = 2048, tickRows = 6, envRows = 24, barsPerStrip = 10;
    int hop = SR / 20, strips = Bars / barsPerStrip, stripH = tickRows + envRows + rows + 4;
    int width = (int)Math.Ceiling(barsPerStrip * 4 * beatSec * 20), height = strips * stripH;
    var rgb = new byte[width * height * 3];
    void Put(int x, int y, (byte r, byte g, byte b) c) { int o = (y * width + x) * 3; rgb[o] = c.r; rgb[o + 1] = c.g; rgb[o + 2] = c.b; }

    var freqs = Enumerable.Range(0, rows).Select(k => 60 * Math.Pow(10000.0 / 60, k / (double)(rows - 1))).ToArray();
    int half = win / 2;                                  // every 2nd sample of a 2048 window (46 ms)
    var cosT = new double[rows * half]; var sinT = new double[rows * half]; var hann = new double[half];
    for (int n = 0; n < half; n++) hann[n] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / (half - 1));
    for (int k = 0; k < rows; k++)
        for (int n = 0; n < half; n++) { double w = 2 * Math.PI * freqs[k] * 2 * n / SR; cosT[k * half + n] = Math.Cos(w); sinT[k * half + n] = Math.Sin(w); }

    int cols = loopLen / hop;
    var mono = new double[half];
    double maxRms = 1e-9;
    var colRms = new double[cols];
    for (int c = 0; c < cols; c++)
    {
        double s = 0; for (int i = c * hop; i < (c + 1) * hop; i++) s += 0.5 * (l[i] * l[i] + r[i] * r[i]);
        colRms[c] = Math.Sqrt(s / hop); maxRms = Math.Max(maxRms, colRms[c]);
    }
    for (int c = 0; c < cols; c++)
    {
        int strip = c / width, x = c % width; if (strip >= strips) break;
        int y0 = strip * stripH;
        int s0 = c * hop + hop / 2 - win / 2;
        for (int n = 0; n < half; n++) { int i = ((s0 + 2 * n) % loopLen + loopLen) % loopLen; mono[n] = 0.5 * (l[i] + r[i]) * hann[n]; }
        for (int k = 0; k < rows; k++)
        {
            double re = 0, im = 0;
            for (int n = 0; n < half; n++) { re += mono[n] * cosT[k * half + n]; im += mono[n] * sinT[k * half + n]; }
            double db = 20 * Math.Log10(Math.Sqrt(re * re + im * im) / (half / 4.0) + 1e-9);
            Put(x, y0 + tickRows + envRows + (rows - 1 - k), Heat((db + 75) / 75));
        }
        double envDb = Db(colRms[c] / maxRms);                      // -36..0 dB -> bar height
        int h = (int)Math.Round(Math.Clamp((envDb + 36) / 36, 0, 1) * (envRows - 2));
        for (int y = 0; y < envRows; y++) Put(x, y0 + tickRows + y, envRows - 1 - y <= h && y > 0 ? ((byte)90, (byte)200, (byte)90) : ((byte)24, (byte)24, (byte)28));
        for (int y = 0; y < tickRows; y++) Put(x, y0 + y, ((byte)10, (byte)10, (byte)14));
    }
    int[] sectionStarts = { 1, 5, 13, 21, 29, 37 };
    for (int bar = 1; bar <= Bars; bar++)
    {
        int strip = (bar - 1) / barsPerStrip, x = (int)Math.Round(((bar - 1) % barsPerStrip) * 4 * beatSec * 20);
        bool sectionStart = sectionStarts.Contains(bar);
        for (int y = sectionStart ? 0 : 3; y < tickRows; y++) Put(Math.Min(x, width - 1), strip * stripH + y, sectionStart ? ((byte)255, (byte)220, (byte)80) : ((byte)200, (byte)200, (byte)200));
    }
    WritePng(path, width, height, rgb);
    Console.WriteLine($"spectrogram: {path} ({width}x{height}; strips of {barsPerStrip} bars, yellow ticks = section starts)");
}

(byte, byte, byte) Heat(double v)
{
    v = Math.Clamp(v, 0, 1);
    double r = Math.Clamp(3 * v - 1, 0, 1), g = Math.Clamp(3 * v - 2, 0, 1), b = v < 1 / 3.0 ? 3 * v : Math.Clamp(2 - 3 * v, 0, 1);
    return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
}

void WritePng(string path, int w, int h, byte[] rgb)
{
    var raw = new byte[h * (w * 3 + 1)];
    for (int y = 0; y < h; y++) Array.Copy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3);   // filter byte 0 per row
    byte[] idat;
    using (var ms = new MemoryStream())
    {
        using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        idat = ms.ToArray();
    }
    var ihdr = new byte[13];
    BigEndian(ihdr, 0, w); BigEndian(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 2;   // 8-bit RGB
    using var fs = File.Create(path);
    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    Chunk(fs, "IHDR", ihdr); Chunk(fs, "IDAT", idat); Chunk(fs, "IEND", Array.Empty<byte>());
}

void Chunk(Stream s, string type, byte[] data)
{
    var len = new byte[4]; BigEndian(len, 0, data.Length); s.Write(len);
    var td = new byte[4 + data.Length]; System.Text.Encoding.ASCII.GetBytes(type).CopyTo(td, 0); data.CopyTo(td, 4);
    s.Write(td);
    var crc = new byte[4]; BigEndian(crc, 0, (int)Crc32(td)); s.Write(crc);
}

void BigEndian(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

uint Crc32(byte[] data)
{
    uint c = 0xFFFFFFFF;
    foreach (byte x in data) { c ^= x; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; }
    return c ^ 0xFFFFFFFF;
}

sealed class Biquad
{
    const double SR = 44100;
    double b0, b1, b2, a1, a2, z1, z2;
    public void LowPass(double f, double q) { Set(f, q, out double c, out double al); Norm((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + al, -2 * c, 1 - al); }
    public void HighPass(double f, double q) { Set(f, q, out double c, out double al); Norm((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + al, -2 * c, 1 - al); }
    public void BandPass(double f, double q) { Set(f, q, out double c, out double al); Norm(al, 0, -al, 1 + al, -2 * c, 1 - al); }
    public double Run(double x) { double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
    static void Set(double f, double q, out double cos, out double alpha)
    {
        double w = 2 * Math.PI * Math.Clamp(f, 10, SR * 0.45) / SR;
        cos = Math.Cos(w); alpha = Math.Sin(w) / (2 * q);
    }
    void Norm(double nb0, double nb1, double nb2, double a0, double na1, double na2) { b0 = nb0 / a0; b1 = nb1 / a0; b2 = nb2 / a0; a1 = na1 / a0; a2 = na2 / a0; }
}
