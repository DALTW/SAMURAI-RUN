// Samurai Run sound effect generator (procedural, no external samples).
// Usage:  dotnet run --project Tools/SfxGen -- <output folder>
// Writes 16-bit 44.1 kHz mono WAV files whose names match SfxId in Assets/Scripts/Audio/SfxId.cs.
// Every sound is built from oscillators, filtered noise, modal (bell/metal/wood) partials,
// a plucked-string model and a small reverb. Tweak a sound by editing its function below and re-running.

const int SR = 44100;
string outDir = args.Length > 0 ? args[0] : "sfx_out";
Directory.CreateDirectory(outDir);
var rng = new Random(20261005);
double Noise() => rng.NextDouble() * 2.0 - 1.0;

var sounds = new (string name, Func<float[]> make)[]
{
    ("swing", Swing), ("parry", Parry), ("just_parry", JustParry),
    ("jump", Jump), ("land", Land), ("footstep", Footstep),
    ("hurt", Hurt), ("death", Death), ("fall", Fall), ("heal", Heal), ("pickup_drop", PickupDrop),
    ("bow_shot", BowShot), ("shuriken_throw", ShurikenThrow), ("enemy_death", EnemyDeath),
    ("log_swing", LogSwing), ("log_hit", LogHit),
    ("speed_up", SpeedUp), ("ui_start", UiStart), ("game_over", GameOver),
};

Console.WriteLine($"{"name",-16}{"sec",6}{"peak(raw)",11}{"rms dBFS",10}");
var finished = new List<float[]>();
foreach (var (name, make) in sounds)
{
    float[] buf = make();
    double rawPeak = Peak(buf);
    Finish(buf);
    WriteWav(Path.Combine(outDir, name + ".wav"), buf);
    finished.Add(buf);
    Console.WriteLine($"{name,-16}{buf.Length / (double)SR,6:0.00}{Db(rawPeak),11:0.0}{Db(ActiveRms(buf)),10:0.0}");
}
// Optional: spectrogram contact sheet (one 96px row per sound, 10 ms per column, 80 Hz - 12 kHz log scale)
int specIdx = Array.IndexOf(args, "--spectrogram");
if (specIdx >= 0 && specIdx + 1 < args.Length) WriteSpectrogramSheet(args[specIdx + 1], finished);
return;

void WriteSpectrogramSheet(string path, List<float[]> bufs)
{
    const int rowH = 96, hop = 441, win = 1024;
    int width = bufs.Max(b => b.Length / hop + 1) + 2;
    int height = bufs.Count * (rowH + 2);
    var img = new byte[width * height];
    var freqs = Enumerable.Range(0, rowH).Select(k => 80 * Math.Pow(12000.0 / 80, k / (double)(rowH - 1))).ToArray();
    var hann = Enumerable.Range(0, win).Select(n => 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / (win - 1))).ToArray();
    for (int s = 0; s < bufs.Count; s++)
    {
        var b = bufs[s];
        int y0 = s * (rowH + 2);
        for (int col = 0; col * hop < b.Length; col++)
        {
            int start = col * hop - win / 2;
            for (int k = 0; k < rowH; k++)
            {
                double w = 2 * Math.PI * freqs[k] / SR, re = 0, im = 0;
                for (int n = 0; n < win; n++)
                {
                    int i = start + n; if (i < 0 || i >= b.Length) continue;
                    double v = b[i] * hann[n]; re += v * Math.Cos(w * n); im -= v * Math.Sin(w * n);
                }
                double db = 20 * Math.Log10(Math.Sqrt(re * re + im * im) / (win / 4.0) + 1e-9);
                int g = (int)Math.Clamp((db + 80) / 80 * 255, 0, 255);
                img[(y0 + (rowH - 1 - k)) * width + col + 1] = (byte)g;
            }
        }
        for (int x = 0; x < width; x++) img[(y0 + rowH) * width + x] = 60; // separator
    }
    // 8-bit grayscale BMP
    int stride = (width + 3) & ~3;
    using var w2 = new BinaryWriter(File.Create(path));
    int dataSize = stride * height, offset = 14 + 40 + 256 * 4;
    w2.Write((byte)'B'); w2.Write((byte)'M'); w2.Write(offset + dataSize); w2.Write(0); w2.Write(offset);
    w2.Write(40); w2.Write(width); w2.Write(height); w2.Write((short)1); w2.Write((short)8); w2.Write(0); w2.Write(dataSize); w2.Write(2835); w2.Write(2835); w2.Write(256); w2.Write(0);
    for (int c = 0; c < 256; c++) { w2.Write((byte)c); w2.Write((byte)c); w2.Write((byte)c); w2.Write((byte)0); }
    var row = new byte[stride];
    for (int y = height - 1; y >= 0; y--) { Array.Clear(row); Array.Copy(img, y * width, row, 0, width); w2.Write(row); }
    Console.WriteLine($"spectrogram sheet: {path} ({width}x{height})");
}

// ======================= sounds =======================

// Sword swing: noise through a band-pass whose center rises then falls as the blade passes.
float[] Swing()
{
    var b = Buf(0.30); var bp1 = new Biquad(); var bp2 = new Biquad();
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        double fc = t < 0.10 ? Lerp(700, 3000, t / 0.10) : Lerp(3000, 1400, Math.Min(1, (t - 0.10) / 0.18));
        bp1.BandPass(fc, 1.4); bp2.BandPass(fc * 2.1, 2.0);
        double n = Noise();
        double a = t < 0.06 ? Smooth(t / 0.06) : Math.Exp(-(t - 0.06) / 0.07);
        b[i] = (float)((bp1.Run(n) + 0.35 * bp2.Run(n)) * a);
    }
    return b;
}

// Sword clash: inharmonic metal partials + a short bright click.
float[] Parry()
{
    var b = Modal(0.8, 1250, new[] { 1.0, 2.32, 3.81, 5.12, 6.95, 1.004 }, new[] { 1.0, 0.55, 0.42, 0.28, 0.18, 0.5 },
                  new[] { 0.20, 0.13, 0.09, 0.06, 0.045, 0.20 }, 0.001);
    AddClick(b, 2000, 0.006, 0.8, highPass: true);
    return Reverb(b, 0.12);
}

// Just parry: brighter, longer ring with a rising shimmer ("kiiin!").
float[] JustParry()
{
    var b = Modal(1.4, 1650, new[] { 1.0, 2.0, 2.76, 4.07, 5.4, 1.0035 }, new[] { 1.0, 0.5, 0.45, 0.3, 0.2, 0.6 },
                  new[] { 0.5, 0.33, 0.22, 0.14, 0.09, 0.5 }, 0.001);
    AddClick(b, 3000, 0.005, 0.9, highPass: true);
    double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i); if (t > 0.25) break;
        double f = Lerp(2200, 4400, Math.Min(1, t / 0.18));
        ph += 2 * Math.PI * f / SR;
        b[i] += (float)(0.25 * Math.Sin(ph) * Env(t, 0.005, 0.12));
    }
    return Reverb(b, 0.22);
}

// Jump: soft rising triangle blip with a breath of air.
float[] Jump()
{
    var b = Buf(0.16); var bp = new Biquad(); bp.BandPass(2500, 1.0); double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        ph += 300 * Math.Pow(2, t / 0.12) / SR;
        b[i] = (float)(Tri(ph) * Env(t, 0.005, 0.05) + 0.15 * bp.Run(Noise()) * Math.Exp(-t / 0.04));
    }
    return b;
}

// Land: low thud.
float[] Land()
{
    var b = Buf(0.14); var lp = new Biquad(); lp.LowPass(900, 0.7); double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        ph += 2 * Math.PI * (55 + 85 * Math.Exp(-t / 0.03)) / SR;
        b[i] = (float)(Math.Sin(ph) * Math.Exp(-t / 0.05) + 0.6 * lp.Run(Noise()) * Math.Exp(-t / 0.02));
    }
    return b;
}

// Footstep: short gravel tick with a tiny thump.
float[] Footstep()
{
    var b = Buf(0.07); var bp = new Biquad(); bp.BandPass(1600, 0.9);
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        b[i] = (float)(bp.Run(Noise()) * Math.Exp(-t / 0.012) + 0.5 * Math.Sin(2 * Math.PI * 95 * t) * Math.Exp(-t / 0.018));
    }
    return b;
}

// Hurt: falling square "oof" + impact noise.
float[] Hurt()
{
    var b = Buf(0.32); var lp = new Biquad(); lp.LowPass(3500, 0.7); var nlp = new Biquad(); nlp.LowPass(2500, 0.7); double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        ph += 430 * Math.Pow(0.33, t / 0.25) / SR;
        double sq = lp.Run(Square(ph)) * Env(t, 0.003, 0.11);
        b[i] = (float)SoftClip(1.3 * (sq + 0.7 * nlp.Run(Noise()) * Math.Exp(-t / 0.04)));
    }
    return b;
}

// Death: heavy boom, crunch and a long falling tone.
float[] Death()
{
    var b = Buf(1.3); var nlp = new Biquad(); nlp.LowPass(1400, 0.7); var slp = new Biquad(); slp.LowPass(2500, 0.7);
    double ph1 = 0, ph2 = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        ph1 += 2 * Math.PI * (40 + 70 * Math.Exp(-t / 0.12)) / SR;
        ph2 += 300 * Math.Pow(0.23, t / 0.8) / SR;
        b[i] = (float)(Math.Sin(ph1) * Math.Exp(-t / 0.3)
                     + 0.7 * nlp.Run(Noise()) * Math.Exp(-t / 0.07)
                     + 0.25 * slp.Run(Square(ph2)) * Math.Exp(-t / 0.35));
    }
    return Reverb(b, 0.18);
}

// Fall: cartoon falling whistle with growing wind.
float[] Fall()
{
    var b = Buf(1.05); var bp = new Biquad(); double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        double f = 1400 * Math.Pow(250.0 / 1400, Math.Min(1, t / 0.95)) * (1 + 0.012 * Math.Sin(2 * Math.PI * 7 * t));
        ph += 2 * Math.PI * f / SR;
        double a = Math.Min(1, t / 0.02) * (t < 0.75 ? 1 : Math.Max(0, 1 - (t - 0.75) / 0.28));
        bp.BandPass(600 + 400 * t, 0.7);
        b[i] = (float)(0.8 * Math.Sin(ph) * a + 0.25 * bp.Run(Noise()) * Math.Min(1, t / 0.4) * a);
    }
    return b;
}

// Heal: bright four-note chime (E major arpeggio).
float[] Heal()
{
    var b = Buf(1.1);
    double[] notes = { 1318.5, 1661.2, 1975.5, 2637.0 };
    for (int k = 0; k < notes.Length; k++)
    {
        int start = (int)(k * 0.065 * SR);
        for (int i = start; i < b.Length; i++)
        {
            double t = T(i - start), p = notes[k] * t;
            b[i] += (float)((0.6 * Math.Sin(2 * Math.PI * p) + 0.4 * Tri(p)) * Env(t, 0.004, 0.22) * 0.5);
        }
    }
    return Reverb(b, 0.25);
}

// Onigiri drop: small rising pop.
float[] PickupDrop()
{
    var b = Buf(0.13); double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        ph += 2 * Math.PI * (380 + 520 * (1 - Math.Exp(-t / 0.02))) / SR;
        b[i] = (float)(Math.Sin(ph) * Env(t, 0.002, 0.04));
    }
    AddClick(b, 3000, 0.002, 0.3, highPass: true);
    return b;
}

// Bow: plucked string twang + arrow whoosh.
float[] BowShot()
{
    var b = Pluck(0.4, 160, 0.996);
    for (int i = 0; i < b.Length; i++) b[i] *= (float)Math.Exp(-T(i) / 0.12);
    AddClick(b, 2000, 0.004, 0.6, highPass: true);
    var bp = new Biquad();
    for (int i = (int)(0.01 * SR); i < b.Length; i++)
    {
        double t = T(i) - 0.01;
        bp.BandPass(Lerp(3500, 1600, Math.Min(1, t / 0.25)), 1.6);
        b[i] += (float)(0.35 * bp.Run(Noise()) * Env(t, 0.02, 0.08));
    }
    return b;
}

// Shuriken: spinning metallic whirr + tiny ting.
float[] ShurikenThrow()
{
    var b = Buf(0.38); var bp = new Biquad(); bp.BandPass(4800, 4);
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        double whirr = bp.Run(Noise()) * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 26 * t)) * Env(t, 0.03, 0.12);
        double ting = (Math.Sin(2 * Math.PI * 2650 * t) + 0.5 * Math.Sin(2 * Math.PI * 4100 * t)) * Math.Exp(-t / 0.05) * 0.5;
        b[i] = (float)(1.6 * whirr + ting);
    }
    return b;
}

// Enemy defeated: slash + body thud + falling retro tone.
float[] EnemyDeath()
{
    var b = Buf(0.55); var hp = new Biquad(); hp.HighPass(2500, 0.7); var lp = new Biquad(); lp.LowPass(3000, 0.7);
    double ph1 = 0, ph2 = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        ph1 += 2 * Math.PI * (60 + 110 * Math.Exp(-t / 0.04)) / SR;
        ph2 += 520 * Math.Pow(0.25, Math.Min(1, t / 0.35)) / SR;
        b[i] = (float)(0.9 * hp.Run(Noise()) * Math.Exp(-t / 0.03)
                     + Math.Sin(ph1) * Math.Exp(-t / 0.1)
                     + 0.3 * lp.Run(Square(ph2)) * Math.Exp(-t / 0.18));
    }
    return b;
}

// Swinging log released: rope creak, then a heavy whoosh that swells as it comes down.
float[] LogSwing()
{
    var b = Buf(0.9); var creakBp = new Biquad(); creakBp.BandPass(650, 2); var bp = new Biquad(); double ph = 0, rumble = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        double s = 0;
        if (t < 0.15)
        {
            ph += (75 + 8 * Noise()) / SR;
            s += 0.5 * creakBp.Run(Saw(ph)) * Env(t, 0.01, 0.06);
        }
        if (t > 0.1)
        {
            double u = t - 0.1;
            double fc = u < 0.6 ? Lerp(250, 900, u / 0.6) : Lerp(900, 400, Math.Min(1, (u - 0.6) / 0.2));
            bp.BandPass(fc, 1.3);
            double a = u < 0.6 ? Smooth(u / 0.6) : Math.Exp(-(u - 0.6) / 0.08);
            rumble += 2 * Math.PI * 65 / SR;
            s += (bp.Run(Noise()) + 0.2 * Math.Sin(rumble)) * a;
        }
        b[i] = (float)s;
    }
    return b;
}

// Log knocked back by the sword: hollow wood "tok".
float[] LogHit()
{
    var b = Modal(0.28, 190, new[] { 1.0, 2.26, 4.16, 7.2 }, new[] { 1.0, 0.7, 0.5, 0.3 }, new[] { 0.09, 0.05, 0.03, 0.015 }, 0.0005);
    AddClick(b, 4000, 0.004, 0.8, highPass: false);
    for (int i = 0; i < b.Length; i++) b[i] = (float)SoftClip(1.4 * b[i]);
    return b;
}

// Speed level up: two quick rising notes.
float[] SpeedUp()
{
    var b = Buf(0.26); var lp = new Biquad(); lp.LowPass(3500, 0.7); double ph = 0;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i);
        bool second = t >= 0.08;
        ph += (second ? 990 : 660) / (double)SR;
        double tn = second ? t - 0.08 : t;
        b[i] = (float)(lp.Run(Square(ph)) * Env(tn, 0.003, 0.06));
    }
    return b;
}

// Title START: blade drawn from its sheath, then a clean ring.
float[] UiStart()
{
    var b = Buf(1.3); var bp = new Biquad();
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i); if (t > 0.3) break;
        bp.BandPass(Lerp(1500, 6500, Math.Min(1, t / 0.22)), 3);
        b[i] += (float)(bp.Run(Noise()) * Env(t, 0.02, 0.12) * 1.5);
    }
    var ring = Modal(1.15, 2100, new[] { 1.0, 2.42, 3.95, 1.003 }, new[] { 1.0, 0.5, 0.3, 0.6 }, new[] { 0.4, 0.25, 0.15, 0.4 }, 0.002);
    FadeEnd(ring, 0.15);
    int off = (int)(0.12 * SR);
    for (int i = 0; i < ring.Length && i + off < b.Length; i++) b[i + off] += 0.8f * ring[i];
    return Reverb(b, 0.3);
}

// Game over: low temple gong.
float[] GameOver()
{
    var b = Buf(2.8); var lp = new Biquad(); lp.LowPass(600, 0.7);
    double[] r = { 1, 2.01, 2.76, 3.9, 5.43, 7.1 }, a = { 1, 0.6, 0.5, 0.35, 0.25, 0.15 }, tau = { 1.4, 1.0, 0.8, 0.5, 0.35, 0.25 };
    var ph = new double[r.Length];
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i), f0 = 98 * (1 + 0.02 * Math.Exp(-t / 0.1)), s = 0;
        for (int k = 0; k < r.Length; k++) { ph[k] += 2 * Math.PI * f0 * r[k] / SR; s += a[k] * Math.Sin(ph[k]) * Math.Exp(-t / tau[k]); }
        b[i] = (float)(s * Math.Min(1, t / 0.004) + 0.6 * lp.Run(Noise()) * Math.Exp(-t / 0.03));
    }
    return Reverb(b, 0.25);
}

// ======================= building blocks =======================

float[] Buf(double seconds) => new float[(int)(seconds * SR)];
double T(int i) => i / (double)SR;
double Lerp(double a, double b, double t) => a + (b - a) * t;
double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
double Env(double t, double attack, double tau) => t < 0 ? 0 : t < attack ? t / attack : Math.Exp(-(t - attack) / tau);
double Frac(double p) => p - Math.Floor(p);
double Square(double p) => Frac(p) < 0.5 ? 1 : -1;
double Saw(double p) => 2 * Frac(p) - 1;
double Tri(double p) => 1 - 4 * Math.Abs(Frac(p) - 0.5);
double SoftClip(double x) => Math.Tanh(x);

// Bell / metal / wood: decaying sine partials at (often inharmonic) ratios.
float[] Modal(double seconds, double f0, double[] ratios, double[] amps, double[] taus, double attack)
{
    var b = Buf(seconds);
    var phase0 = new double[ratios.Length];
    for (int k = 0; k < ratios.Length; k++) phase0[k] = rng.NextDouble() * 2 * Math.PI;
    for (int i = 0; i < b.Length; i++)
    {
        double t = T(i), s = 0;
        for (int k = 0; k < ratios.Length; k++) s += amps[k] * Math.Sin(2 * Math.PI * f0 * ratios[k] * t + phase0[k]) * Math.Exp(-t / taus[k]);
        b[i] = (float)(s * Math.Min(1, t / Math.Max(1e-6, attack)));
    }
    return b;
}

// Short filtered noise transient added at the start.
void AddClick(float[] b, double cutoff, double tau, double gain, bool highPass)
{
    var f = new Biquad(); if (highPass) f.HighPass(cutoff, 0.7); else f.LowPass(cutoff, 0.7);
    int n = Math.Min(b.Length, (int)(tau * 8 * SR));
    for (int i = 0; i < n; i++) b[i] += (float)(gain * f.Run(Noise()) * Math.Exp(-T(i) / tau));
}

// Karplus-Strong plucked string.
float[] Pluck(double seconds, double freq, double decay)
{
    var b = Buf(seconds);
    int n = (int)(SR / freq);
    var line = new double[n];
    for (int i = 0; i < n; i++) line[i] = Noise();
    int idx = 0;
    for (int i = 0; i < b.Length; i++)
    {
        int next = (idx + 1) % n;
        double y = decay * 0.5 * (line[idx] + line[next]);
        b[i] = (float)line[idx];
        line[idx] = y;
        idx = next;
    }
    return b;
}

// Small Schroeder reverb (4 damped combs + 2 allpasses), mixed in and given a short tail.
float[] Reverb(float[] dry, double mix)
{
    FadeEnd(dry, 0.08); // 울림이 남은 채 잘리면 '딱' 소리가 나므로 끝을 부드럽게
    int tail = (int)(0.35 * SR);
    var outp = new float[dry.Length + tail];
    int[] combLen = { 1116, 1188, 1277, 1356 }; int[] apLen = { 556, 441 };
    var combs = combLen.Select(l => new double[l]).ToArray(); var cIdx = new int[4]; var cStore = new double[4];
    var aps = apLen.Select(l => new double[l]).ToArray(); var aIdx = new int[2];
    for (int i = 0; i < outp.Length; i++)
    {
        double x = i < dry.Length ? dry[i] : 0, wet = 0;
        for (int c = 0; c < 4; c++)
        {
            double y = combs[c][cIdx[c]];
            cStore[c] = y * 0.8 + cStore[c] * 0.2;              // damping
            combs[c][cIdx[c]] = x * 0.25 + cStore[c] * 0.78;    // feedback
            cIdx[c] = (cIdx[c] + 1) % combs[c].Length;
            wet += y;
        }
        for (int a = 0; a < 2; a++)
        {
            double buf = aps[a][aIdx[a]];
            double y = -wet + buf;
            aps[a][aIdx[a]] = wet + buf * 0.5;
            aIdx[a] = (aIdx[a] + 1) % aps[a].Length;
            wet = y;
        }
        outp[i] = (float)(x + mix * wet);
    }
    return outp;
}

void FadeEnd(float[] b, double seconds)
{
    int n = Math.Min(b.Length, (int)(seconds * SR));
    for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= (float)Smooth(i / (double)n);
}

double Peak(float[] b) { double p = 0; foreach (var v in b) p = Math.Max(p, Math.Abs(v)); return p; }
double Db(double v) => 20 * Math.Log10(Math.Max(1e-9, v));
double ActiveRms(float[] b)
{
    // RMS over samples louder than -40 dB of the peak (ignores silent tails)
    double peak = Peak(b), sum = 0; int n = 0;
    foreach (var v in b) if (Math.Abs(v) > peak * 0.01) { sum += v * v; n++; }
    return n == 0 ? 0 : Math.Sqrt(sum / n);
}

// DC removal, 1 ms fade-in, 15 ms fade-out, peak normalize to -1 dBFS.
void Finish(float[] b)
{
    double prevX = 0, prevY = 0, r = 1 - 2 * Math.PI * 20 / SR;
    for (int i = 0; i < b.Length; i++) { double y = b[i] - prevX + r * prevY; prevX = b[i]; prevY = y; b[i] = (float)y; }
    int fi = SR / 1000, fo = (int)(0.015 * SR);
    for (int i = 0; i < Math.Min(fi, b.Length); i++) b[i] *= i / (float)fi;
    for (int i = 0; i < Math.Min(fo, b.Length); i++) b[b.Length - 1 - i] *= i / (float)fo;
    double peak = Peak(b);
    if (peak > 0) { float g = (float)(0.891 / peak); for (int i = 0; i < b.Length; i++) b[i] *= g; }
}

void WriteWav(string path, float[] b)
{
    using var w = new BinaryWriter(File.Create(path));
    int dataBytes = b.Length * 2;
    w.Write("RIFF"u8.ToArray()); w.Write(36 + dataBytes); w.Write("WAVE"u8.ToArray());
    w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
    w.Write("data"u8.ToArray()); w.Write(dataBytes);
    foreach (var v in b) w.Write((short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767));
}

// RBJ biquad (transposed direct form II); coefficients can change every sample.
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
