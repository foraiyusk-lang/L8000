"""
残り8000回 - BGM (逆再生対応)
  MIDI で作曲(scripts/midi/bgm.mid) → 柔らかいシンセ＋プレートリバーブ。
  出力:
    Assets/Resources/Audio/bgm.wav      … 順再生
    Assets/Resources/Audio/bgm_rev.wav  … 逆再生(サンプルを反転しただけ)
  Unity は圧縮/ストリーミング再生だと負ピッチ再生が不安定なので、
  逆再生は「反転済み wav を鳴らす」方式にする。

  逆再生でも違和感が無いよう:
    ・鋭いアタックを使わない(全部スウェル)
    ・エンベロープは概ね左右対称(三角)
    ・旋律は最小限、ドローン/パッド主体
  A natural minor / Am(9) - Fmaj7 - Cmaj7 - Gsus2 を4周(≈2分)。

deps: mido, numpy   (pip install mido)
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
WAV_FWD = os.path.join(WAV_DIR, "bgm.wav")
WAV_REV = os.path.join(WAV_DIR, "bgm_rev.wav")

BPM = 60
TPB = 480
SPB = 60.0 / BPM
SEG_BEATS = 8.0          # 1コード = 2小節

CHORDS = [
    [45, 57, 60, 64, 71],   # Am(9) : A2 A3 C4 E4 B4
    [41, 53, 57, 60, 64],   # Fmaj7
    [48, 55, 59, 60, 64],   # Cmaj7
    [43, 55, 57, 62, 67],   # Gsus2
]
# ごく控えめな鐘(セクション3のみ)。三角エンベロープ。
BELLS = {2: [(1.0, 72), (5.0, 79)]}
N_SECTIONS = 4


def build_midi_and_events():
    mid = mido.MidiFile(ticks_per_beat=TPB)
    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(BPM), time=0))
    meta.append(mido.MetaMessage("track_name", name="Zan8000 BGM (rev-safe)", time=0))
    pad = mido.MidiTrack(); mid.tracks.append(pad)
    pad.append(mido.Message("program_change", program=89, time=0))

    notes = {"pad": [], "bell": []}
    beat = 0.0
    for s in range(N_SECTIONS):
        for ci in range(4):
            for n in CHORDS[ci]:
                notes["pad"].append((beat, SEG_BEATS, n, 40))
            for (off, mn) in BELLS.get(s, []):
                notes["bell"].append((beat + off, 4.0, mn, 34))
            beat += SEG_BEATS

    ev = []
    for (st, du, n, v) in notes["pad"]:
        ev.append((st, "note_on", n, v)); ev.append((st + du, "note_off", n, 0))
    ev.sort(); prev = 0.0
    for (b, typ, n, v) in ev:
        pad.append(mido.Message(typ, note=n, velocity=v, time=int(round((b - prev) * TPB))))
        prev = b
    os.makedirs(MID_DIR, exist_ok=True)
    mid.save(MID_PATH)
    return notes, beat * SPB


def tri_env(n, edge=0.45):
    """左右対称の三角(スウェル→スウェル)。逆再生でも同じ形。"""
    e = np.ones(n)
    a = int(n * edge)
    if a > 0:
        e[:a] = np.linspace(0, 1, a)
        e[-a:] = np.linspace(1, 0, a)
    return e ** 1.4


def v_pad(f, dur, vel):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n)
    for det in (-0.13, 0.0, 0.14):
        ff = f * (1 + det / 100.0)
        for k, amp in ((1, 1.0), (2, 0.28), (3, 0.11), (5, 0.04)):
            sig += amp * np.sin(2 * np.pi * ff * k * t + k * 0.7)
    sig *= 1.0 + 0.05 * np.sin(2 * np.pi * 0.09 * t + f)   # ゆっくりうねり
    sig = dsp.one_pole_lp(sig * tri_env(n, 0.45), 2200.0)
    return sig * (vel / 127.0) * 0.11


def v_bell(f, dur, vel):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = (np.sin(2 * np.pi * f * t)
           + 0.4 * np.sin(2 * np.pi * 2 * f * t)
           + 0.18 * np.sin(2 * np.pi * 3.01 * f * t))
    return sig * tri_env(n, 0.5) * (vel / 127.0) * 0.16


def render(notes, total_sec):
    buf = np.zeros(int((total_sec + 2.0) * SR))
    send = np.zeros_like(buf)
    for kind, lst in notes.items():
        fn = v_pad if kind == "pad" else v_bell
        rv = 0.4 if kind == "pad" else 0.85
        for (st, du, n, vel) in lst:
            sig = fn(hz(n), du * SPB, vel)
            i = int(st * SPB * SR); j = min(len(buf), i + len(sig))
            buf[i:j] += sig[:j - i]
            send[i:j] += sig[:j - i] * rv

    ir = dsp.make_ir(decay=0.6, length=2.4, lp=3000.0)
    buf = buf + 0.5 * dsp.reverb(send, ir, mix=1.0)
    buf = dsp.one_pole_lp(buf, 6000.0)
    buf = dsp.one_pole_hp(buf, 30.0)

    loop = int(total_sec * SR)
    xf = int(0.5 * SR)
    body = buf[:loop].copy()
    spill = buf[loop:loop + xf]
    if len(spill) == xf:
        body[:xf] += spill * np.linspace(1, 0, xf)
    fe = int(0.02 * SR)
    body[:fe] *= np.linspace(0, 1, fe)
    body[-fe:] *= np.linspace(1, 0, fe)

    body = dsp.normalize(body, 0.62)
    body = body[::2]                      # 22.05kHz
    fwd = (dsp.soft_limit(body) * 32767 * 0.95).astype("<i2")
    rev = fwd[::-1].copy()

    for path, data in ((WAV_FWD, fwd), (WAV_REV, rev)):
        with wave.open(path, "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR // 2)
            w.writeframes(data.tobytes())
    return loop / SR


if __name__ == "__main__":
    notes, total = build_midi_and_events()
    loop = render(notes, total)
    print(f"MIDI    : {MID_PATH}")
    print(f"WAV fwd : {WAV_FWD}")
    print(f"WAV rev : {WAV_REV}  ({loop:.1f}s loop, {BPM} BPM)")
