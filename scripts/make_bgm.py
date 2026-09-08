"""
残り8000回 - BGM
  MIDI で作曲(scripts/midi/bgm.mid) → 柔らかいシンセ＋プレートリバーブで WAV
  (Assets/Resources/Audio/bgm.wav)。ループ前提。

  A natural minor / Am(9) - Fmaj7 - Cmaj7 - Gsus2 を4セクション(≈2分)。
  低ベロシティ・不協和なし・余白多め・耳に痛い帯域なし・怖くしない。
    A  : パッド＋ベースのみ(静けさ)
    B  : メロディ(セレスタ)が入る
    C  : パッドにオクターブを重ねて少し厚く ＋ 高音のかすかな揺らめき
    D  : 音数を減らして A へ戻る

usage:  python scripts/make_bgm.py
deps :  mido, numpy   (pip install mido)
"""
import os
import wave

import numpy as np
import mido

import dsp
from dsp import SR, hz

_ROOT = os.path.join(os.path.dirname(__file__), "..")
MID_DIR = os.path.join(_ROOT, "scripts", "midi")
WAV_DIR = os.path.join(_ROOT, "Assets", "Resources", "Audio")
MID_PATH = os.path.join(MID_DIR, "bgm.mid")
WAV_PATH = os.path.join(WAV_DIR, "bgm.wav")

BPM = 63
TPB = 480
SPB = 60.0 / BPM
BAR = 4
CHORD_BARS = 2
SEG_BEATS = CHORD_BARS * BAR

# 和音: 滑らかなボイシングで C4 付近
CHORDS = [
    [57, 60, 64, 71],   # Am(9) : A3 C4 E4 B4
    [53, 57, 60, 64],   # Fmaj7 : F3 A3 C4 E4
    [55, 59, 60, 64],   # Cmaj7 : G3 B3 C4 E4
    [55, 57, 62, 67],   # Gsus2 : G3 A3 D4 G4
]
BASS = [45, 41, 48, 43]   # A2 F2 C3 G3

# メロディ(各コード区間の頭からの相対): (拍, 長さ拍, ノート)
MEL_B = [
    [(0.0, 1.5, 76), (2.0, 1.0, 74), (5.0, 3.0, 72)],
    [(0.0, 2.0, 72), (3.0, 1.0, 69), (5.0, 3.0, 65)],
    [(1.0, 1.0, 67), (2.0, 1.0, 71), (4.0, 4.0, 74)],
    [(0.0, 1.5, 74), (2.0, 1.0, 71), (4.5, 2.0, 69)],
]
MEL_C = [
    [(0.0, 3.0, 72), (4.0, 1.0, 71), (5.0, 3.0, 69)],
    [(0.0, 2.0, 77), (2.5, 1.5, 76), (5.0, 3.0, 72)],
    [(0.0, 2.0, 71), (3.0, 1.0, 72), (4.0, 4.0, 79)],
    [(0.0, 2.0, 74), (3.5, 2.5, 67)],
]
MEL_D = [
    [(2.0, 4.0, 69)],
    [(0.0, 3.0, 65), (4.0, 3.0, 64)],
    [(2.0, 4.0, 67)],
    [(0.0, 6.0, 62)],
]
# section: (chord octave add, melody or None, shimmer bool)
SECTIONS = [
    (False, None,  False),   # A
    (False, MEL_B, False),   # B
    (True,  MEL_C, True),    # C
    (False, MEL_D, False),   # D
]


def build_midi():
    mid = mido.MidiFile(ticks_per_beat=TPB)
    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(BPM), time=0))
    meta.append(mido.MetaMessage("time_signature", numerator=4, denominator=4, time=0))
    meta.append(mido.MetaMessage("track_name", name="Zan8000 BGM", time=0))

    pad = mido.MidiTrack(); mid.tracks.append(pad)
    pad.append(mido.Message("program_change", program=89, time=0))
    mel = mido.MidiTrack(); mid.tracks.append(mel)
    mel.append(mido.Message("program_change", program=8, time=0))
    bass = mido.MidiTrack(); mid.tracks.append(bass)
    bass.append(mido.Message("program_change", program=89, time=0))

    notes = {"pad": [], "mel": [], "bass": [], "shimmer": []}

    def add(kind, note, start, dur, vel):
        notes[kind].append((start, dur, note, vel))

    beat = 0.0
    for (add_oct, melody, shimmer) in SECTIONS:
        for ci in range(4):
            for n in CHORDS[ci]:
                add("pad", n, beat, SEG_BEATS - 0.2, 44)
                if add_oct and n <= 62:
                    add("pad", n + 12, beat, SEG_BEATS - 0.2, 26)
            add("bass", BASS[ci], beat, SEG_BEATS - 0.15, 38)
            if melody:
                for (off, d, mn) in melody[ci]:
                    add("mel", mn, beat + off, d, 50)
            if shimmer:
                for k in range(4):
                    add("shimmer", CHORDS[ci][k % len(CHORDS[ci])] + 24,
                        beat + k * 2.0, 1.6, 18)
            beat += SEG_BEATS

    total_beats = beat

    # MIDI 書き出し(絶対→デルタ)
    for name, tr in (("pad", pad), ("mel", mel), ("bass", bass)):
        ev = []
        for (s, d, n, v) in notes[name]:
            ev.append((s, "note_on", n, v))
            ev.append((s + d, "note_off", n, 0))
        ev.sort()
        prev = 0.0
        for (b, typ, n, v) in ev:
            tr.append(mido.Message(typ, note=n, velocity=v,
                                   time=int(round((b - prev) * TPB))))
            prev = b

    os.makedirs(MID_DIR, exist_ok=True)
    os.makedirs(WAV_DIR, exist_ok=True)
    mid.save(MID_PATH)
    return notes, total_beats * SPB


# ---------------- シンセ ----------------
def env_adsr(n, a, d, s, r):
    e = np.zeros(n)
    ai = min(int(a * SR), n)
    if ai:
        e[:ai] = np.linspace(0, 1, ai)
    di = min(int(d * SR), max(0, n - ai))
    if di:
        e[ai:ai + di] = np.linspace(1, s, di)
    se = max(ai + di, n - int(r * SR))
    e[ai + di:se] = s
    if n - se > 0:
        e[se:] = np.linspace(e[se - 1] if se else s, 0, n - se)
    return e


def v_pad(f, dur, vel):
    n = int((dur + 1.4) * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n)
    # 3 声の微デチューン + わずかな倍音
    for det in (-0.14, 0.0, 0.15):
        ff = f * (1 + det / 100.0)
        for k, amp in ((1, 1.0), (2, 0.3), (3, 0.13), (4, 0.05)):
            sig += amp * np.sin(2 * np.pi * ff * k * t + k * 0.3)
    # ゆっくりしたコーラス(振幅の揺れ)
    sig *= 1.0 + 0.06 * np.sin(2 * np.pi * 0.13 * t)
    e = env_adsr(n, a=0.7, d=0.8, s=0.72, r=1.3)
    sig = dsp.one_pole_lp(sig * e, 2400.0)
    return sig * (vel / 127.0) * 0.12


def v_bell(f, dur, vel):
    n = int((dur + 1.8) * SR)
    t = np.arange(n) / SR
    sig = (np.sin(2 * np.pi * f * t)
           + 0.55 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-3.0 * t)
           + 0.28 * np.sin(2 * np.pi * 3.01 * f * t) * np.exp(-5.5 * t)
           + 0.12 * np.sin(2 * np.pi * 4.02 * f * t) * np.exp(-8.0 * t))
    e = np.exp(-t / 0.85)
    a = int(0.005 * SR)
    e[:a] *= np.linspace(0, 1, a)
    return sig * e * (vel / 127.0) * 0.22


def v_bass(f, dur, vel):
    n = int((dur + 0.5) * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * f * t) + 0.14 * np.sin(2 * np.pi * 2 * f * t)
    e = env_adsr(n, a=0.04, d=0.25, s=0.7, r=0.4)
    return dsp.one_pole_lp(sig * e, 900.0) * (vel / 127.0) * 0.5


def v_shimmer(f, dur, vel):
    n = int((dur + 1.2) * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t)
    e = np.exp(-t / 0.5)
    a = int(0.02 * SR)
    e[:a] *= np.linspace(0, 1, a)
    return sig * e * (vel / 127.0) * 0.2


VOICES = {"pad": v_pad, "mel": v_bell, "bass": v_bass, "shimmer": v_shimmer}


def render(notes, total_sec):
    buf = np.zeros(int((total_sec + 3.0) * SR))
    send = np.zeros_like(buf)   # リバーブ送り(メロディ/シマー多め)

    for kind, lst in notes.items():
        fn = VOICES[kind]
        rv = {"mel": 0.9, "shimmer": 1.0, "pad": 0.35, "bass": 0.0}[kind]
        for (s, d, n, v) in lst:
            sig = fn(hz(n), d * SPB, v)
            i = int(s * SPB * SR)
            j = min(len(buf), i + len(sig))
            buf[i:j] += sig[:j - i]
            send[i:j] += sig[:j - i] * rv

    ir = dsp.make_ir(decay=0.5, length=2.0, lp=3200.0)
    mix = dsp.reverb(send, ir, mix=1.0)          # send は全部 wet
    out = buf + 0.5 * mix
    out = dsp.one_pole_lp(out, 6500.0)
    out = dsp.one_pole_hp(out, 32.0)

    loop_len = int(total_sec * SR)
    xf = int(0.35 * SR)
    body = out[:loop_len].copy()
    spill = out[loop_len:loop_len + xf]
    if len(spill) == xf:
        body[:xf] += spill * np.linspace(1, 0, xf)
    fe = int(0.012 * SR)
    body[:fe] *= np.linspace(0, 1, fe)
    body[-fe:] *= np.linspace(1, 0, fe)

    body = dsp.normalize(body, 0.72)

    # BGM は暗い音なので 22.05kHz で十分。ファイルを半分に(6.5kHz で LP 済み)。
    body = body[::2]
    data = (dsp.soft_limit(dsp.normalize(body, 0.97)) * 32767 * 0.95).astype("<i2")
    with wave.open(WAV_PATH, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR // 2)
        w.writeframes(data.tobytes())
    return loop_len / SR


if __name__ == "__main__":
    notes, total = build_midi()
    loop = render(notes, total)
    print(f"MIDI : {MID_PATH}")
    print(f"WAV  : {WAV_PATH}  ({loop:.1f}s loop, {BPM} BPM)")
