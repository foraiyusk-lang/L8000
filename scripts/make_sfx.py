"""
残り8000回 - 効果音
  BGM と同じ作り: 各 SE を MIDI(scripts/midi/sfx_*.mid)で書き、
  柔らかいシンセ＋軽いリバーブで WAV(Assets/Resources/Audio/sfx_*.wav)へ。
  方針: sine/triangle 主体・低音量・短い・耳に痛い成分なし・怖くしない。

usage: python scripts/make_sfx.py
deps : mido, numpy
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
BPM = 120
TPB = 480

# name: (voice, [(start_beat, dur_beat, midinote, vel), ...], gain, reverb_mix)
SFX = {
    "sfx_jump":  ("bell",  [(0.0, 0.16, 72, 55), (0.09, 0.20, 79, 48)], 0.5, 0.10),
    "sfx_land":  ("thud",  [(0.0, 0.28, 40, 66)], 0.55, 0.06),
    "sfx_shoot": ("pluck", [(0.0, 0.12, 62, 44)], 0.38, 0.05),
    "sfx_hit":   ("bell",  [(0.0, 0.18, 88, 46), (0.05, 0.45, 81, 40)], 0.46, 0.22),
    "sfx_break": ("pad",   [(0.0, 0.5, 41, 60), (0.16, 0.7, 38, 54)], 0.62, 0.30),   # 下降短3度: 何かが失われる
    "sfx_gate":  ("bell",  [(0.0, 0.26, 62, 50), (0.13, 0.36, 67, 50),
                            (0.28, 1.1, 74, 46), (0.28, 1.1, 79, 34)], 0.55, 0.28),  # 4度→上へ: 解錠
}


def v_bell(f, dur, vel):
    n = int((dur + 1.3) * SR); t = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * t)
         + 0.5 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-4 * t)
         + 0.22 * np.sin(2 * np.pi * 3.01 * f * t) * np.exp(-7 * t))
    e = np.exp(-t / 0.5)
    a = int(0.004 * SR); e[:a] *= np.linspace(0, 1, a)
    return s * e * (vel / 127) * 0.6


def v_thud(f, dur, vel):
    n = int((dur + 0.3) * SR); t = np.arange(n) / SR
    fenv = f * (1 + 1.5 * np.exp(-t / 0.03))
    ph = 2 * np.pi * np.cumsum(fenv) / SR
    s = np.sin(ph) + 0.15 * np.sin(2 * ph)
    e = np.exp(-t / 0.10)
    a = int(0.003 * SR); e[:a] *= np.linspace(0, 1, a)
    return s * e * (vel / 127) * 0.9


def v_pluck(f, dur, vel):
    n = int((dur + 0.35) * SR); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * f * t) + 0.4 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-9 * t)
    e = np.exp(-t / 0.09)
    a = int(0.002 * SR); e[:a] *= np.linspace(0, 1, a)
    return s * e * (vel / 127) * 0.55


def v_pad(f, dur, vel):
    n = int((dur + 0.9) * SR); t = np.arange(n) / SR
    s = np.zeros(n)
    for k, amp in ((1, 1.0), (2, 0.4), (3, 0.18)):
        s += amp * np.sin(2 * np.pi * f * k * t)
    e = np.minimum(1.0, t / 0.05) * np.exp(-t / (dur * 0.9))
    return s * e * (vel / 127) * 0.4


VOICES = {"bell": v_bell, "thud": v_thud, "pluck": v_pluck, "pad": v_pad}
_IR = dsp.make_ir(decay=0.35, length=1.2, lp=3600.0)


def build(name, voice, notes, gain, rv):
    spb = 60.0 / BPM
    end = max(s + d for s, d, _, _ in notes) * spb + 1.5
    buf = np.zeros(int(end * SR))
    fn = VOICES[voice]
    for (sb, db, mn, vel) in notes:
        sig = fn(hz(mn), db * spb, vel)
        i = int(sb * spb * SR); j = min(len(buf), i + len(sig))
        buf[i:j] += sig[:j - i]
    buf = dsp.one_pole_lp(buf, 5200.0)
    if rv > 0:
        buf = dsp.reverb(buf, _IR, mix=rv)
    peak = np.max(np.abs(buf)) or 1.0
    buf = dsp.soft_limit(buf / peak * gain, 1.05)
    fe = int(0.006 * SR)
    buf[-fe:] *= np.linspace(1, 0, fe)
    data = (buf * 32767 * 0.9).astype("<i2")

    os.makedirs(WAV_DIR, exist_ok=True)
    with wave.open(os.path.join(WAV_DIR, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(data.tobytes())

    mid = mido.MidiFile(ticks_per_beat=TPB)
    tr = mido.MidiTrack(); mid.tracks.append(tr)
    tr.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(BPM), time=0))
    tr.append(mido.MetaMessage("track_name", name=name, time=0))
    ev = []
    for (sb, db, mn, vel) in notes:
        ev.append((sb, "note_on", mn, vel))
        ev.append((sb + db, "note_off", mn, 0))
    ev.sort()
    prev = 0.0
    for (b, typ, mn, vel) in ev:
        tr.append(mido.Message(typ, note=mn, velocity=vel, time=int(round((b - prev) * TPB))))
        prev = b
    os.makedirs(MID_DIR, exist_ok=True)
    mid.save(os.path.join(MID_DIR, name + ".mid"))


if __name__ == "__main__":
    for name, (voice, notes, gain, rv) in SFX.items():
        build(name, voice, notes, gain, rv)
        print("wrote", name)
