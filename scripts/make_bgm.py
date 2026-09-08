"""
残り8000回 - BGM
  * MIDI で作曲(Assets/Audio/bgm.mid) → 柔らかいシンセで WAV レンダリング(Assets/Audio/bgm.wav)
  * ループ前提。主張しすぎない・不協和なし・静か。
    A natural minor / vi-IV-I-V(Am9 - Fmaj7 - Cmaj7 - Gsus2)を 2 周。
    低ベロシティ、緩いテンポ、余白多め。

usage:  python scripts/make_bgm.py
deps :  mido, numpy   (pip install mido)
"""
import math
import os
import struct
import wave

import numpy as np
import mido

_ROOT = os.path.join(os.path.dirname(__file__), "..")
MID_DIR = os.path.join(_ROOT, "Assets", "Audio")
WAV_DIR = os.path.join(_ROOT, "Assets", "Resources", "Audio")
MID_PATH = os.path.join(MID_DIR, "bgm.mid")
WAV_PATH = os.path.join(WAV_DIR, "bgm.wav")

SR = 44100
BPM = 66
TPB = 480                       # ticks per beat
SEC_PER_BEAT = 60.0 / BPM
BAR = 4                          # 4/4

# --- 和音(MIDI ノート番号)。滑らかなボイシングで C4 付近に集める ---
CHORDS = [
    ("Am(9)",  [57, 60, 64, 71]),   # A3 C4 E4 B4
    ("Fmaj7",  [53, 57, 60, 64]),   # F3 A3 C4 E4
    ("Cmaj7",  [55, 59, 60, 64]),   # G3 B3 C4 E4
    ("Gsus2",  [55, 57, 62, 67]),   # G3 A3 D4 G4
]
BASS = [45, 41, 48, 43]             # A2 F2 C3 G3

# --- メロディ(2小節=1コードごと。休符多め、下降主体で寂しげに) ---
# (拍オフセット, 長さ拍, ノート) ※各コード区間の頭からの相対
MEL_A = [
    [(0.0, 1.5, 76), (2.0, 1.0, 74), (5.0, 3.0, 72)],           # Am : E5 D5 C5
    [(0.0, 2.0, 72), (3.0, 1.0, 69), (5.0, 3.0, 65)],           # F  : C5 A4 F4
    [(1.0, 1.0, 67), (2.0, 1.0, 71), (4.0, 4.0, 74)],           # C  : G4 B4 D5
    [(0.0, 1.5, 74), (2.0, 1.0, 71), (4.0, 2.0, 69)],           # G  : D5 B4 A4  (最後は早めに休符)
]
MEL_B = [
    [(0.0, 3.0, 72), (4.0, 1.0, 71), (5.0, 3.0, 69)],           # Am : C5 B4 A4
    [(0.0, 2.0, 77), (2.5, 1.5, 76), (5.0, 3.0, 72)],           # F  : F5 E5 C5
    [(0.0, 2.0, 71), (3.0, 1.0, 72), (4.0, 4.0, 76)],           # C  : B4 C5 E5
    [(0.0, 2.0, 74), (3.0, 3.0, 67)],                            # G  : D5 G4  (終わりは静かに)
]

CHORD_BARS = 2                       # 1 コード = 2 小節
SECTIONS = [MEL_A, MEL_B]


def build_midi():
    mid = mido.MidiFile(ticks_per_beat=TPB)

    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(BPM), time=0))
    meta.append(mido.MetaMessage("time_signature", numerator=4, denominator=4, time=0))
    meta.append(mido.MetaMessage("track_name", name="Zan8000 BGM", time=0))

    pad = mido.MidiTrack(); mid.tracks.append(pad)
    pad.append(mido.Message("program_change", program=89, time=0))   # Warm Pad
    mel = mido.MidiTrack(); mid.tracks.append(mel)
    mel.append(mido.Message("program_change", program=8, time=0))    # Celesta
    bass = mido.MidiTrack(); mid.tracks.append(bass)
    bass.append(mido.Message("program_change", program=89, time=0))

    def add_note(track, note, start_beat, dur_beat, vel):
        t0 = int(round(start_beat * TPB))
        t1 = int(round((start_beat + dur_beat) * TPB))
        track.append(mido.Message("note_on", note=note, velocity=vel, time=t0))
        track.append(mido.Message("note_off", note=note, velocity=0, time=t1))

    seg_beats = CHORD_BARS * BAR
    beat = 0.0
    for section in SECTIONS:
        for ci, (_, notes) in enumerate(CHORDS):
            for n in notes:
                add_note(pad, n, beat, seg_beats - 0.15, 46)
            add_note(bass, BASS[ci], beat, seg_beats - 0.1, 40)
            for (off, dur, mnote) in section[ci]:
                add_note(mel, mnote, beat + off, dur, 52)
            beat += seg_beats

    # mido は絶対 time で append したので delta へ変換
    for tr in (pad, mel, bass):
        tr.sort(key=lambda m: m.time)
        prev = 0
        for m in tr:
            m.time, prev = m.time - prev, m.time

    os.makedirs(MID_DIR, exist_ok=True)
    os.makedirs(WAV_DIR, exist_ok=True)
    mid.save(MID_PATH)
    total = beat * SEC_PER_BEAT
    return mid, total


# ---------------- 柔らかいシンセで WAV へ ----------------
def adsr(n, a, d, s, r, sr=SR):
    env = np.zeros(n)
    ai, di, ri = int(a * sr), int(d * sr), int(r * sr)
    ai = min(ai, n)
    env[:ai] = np.linspace(0, 1, ai, endpoint=False) if ai else []
    di = min(di, max(0, n - ai))
    if di:
        env[ai:ai + di] = np.linspace(1, s, di, endpoint=False)
    sus_end = max(ai + di, n - ri)
    env[ai + di:sus_end] = s
    ri = n - sus_end
    if ri > 0:
        env[sus_end:] = np.linspace(env[sus_end - 1] if sus_end else 0, 0, ri)
    return env

def hz(note):
    return 440.0 * 2 ** ((note - 69) / 12.0)

def render_pad(freq, dur, vel):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n)
    for k, amp in ((1, 1.0), (2, 0.35), (3, 0.16), (4, 0.06)):
        det = 1.0 + 0.0016 * (k - 1)
        sig += amp * (np.sin(2 * np.pi * freq * k * det * t)
                      + np.sin(2 * np.pi * freq * k / det * t)) * 0.5
    env = adsr(n, a=0.5, d=0.6, s=0.75, r=1.1)
    return sig * env * (vel / 127.0) * 0.16

def render_bell(freq, dur, vel):
    n = int((dur + 1.6) * SR)          # 余韻
    t = np.arange(n) / SR
    sig = (np.sin(2 * np.pi * freq * t)
           + 0.5 * np.sin(2 * np.pi * freq * 2 * t) * np.exp(-3 * t)
           + 0.25 * np.sin(2 * np.pi * freq * 3.01 * t) * np.exp(-5 * t))
    env = np.exp(-t / 0.9)
    atk = int(0.005 * SR)
    env[:atk] *= np.linspace(0, 1, atk)
    return sig * env * (vel / 127.0) * 0.22

def render_bass(freq, dur, vel):
    n = int((dur + 0.4) * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * freq * t) + 0.12 * np.sin(2 * np.pi * freq * 2 * t)
    env = adsr(n, a=0.03, d=0.2, s=0.7, r=0.35)
    return sig * env * (vel / 127.0) * 0.5

def one_pole_lowpass(x, cutoff=4200.0):
    dt = 1.0 / SR
    rc = 1.0 / (2 * np.pi * cutoff)
    a = dt / (rc + dt)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y

def render_wav(mid, total_sec):
    tail = 2.0
    buf = np.zeros(int((total_sec + tail) * SR))

    def place(sig, start_sec):
        i = int(start_sec * SR)
        j = min(len(buf), i + len(sig))
        buf[i:j] += sig[:j - i]

    for ti, tr in enumerate(mid.tracks):
        abs_t = 0.0
        pending = {}
        for msg in tr:
            abs_t += mido.tick2second(msg.time, mid.ticks_per_beat, mido.bpm2tempo(BPM))
            if msg.type == "note_on" and msg.velocity > 0:
                pending[msg.note] = (abs_t, msg.velocity)
            elif msg.type in ("note_off",) or (msg.type == "note_on" and msg.velocity == 0):
                if msg.note in pending:
                    t0, vel = pending.pop(msg.note)
                    dur = max(0.05, abs_t - t0)
                    f = hz(msg.note)
                    if ti == 1:
                        place(render_pad(f, dur, vel), t0)
                    elif ti == 2:
                        place(render_bell(f, dur, vel), t0)
                    elif ti == 3:
                        place(render_bass(f, dur, vel), t0)

    buf = one_pole_lowpass(buf, 4200.0)

    # ループ用: 末尾の余韻を先頭へ折り返して継ぎ目を消す
    loop_len = int(total_sec * SR)
    xfade = int(0.25 * SR)
    body = buf[:loop_len].copy()
    spill = buf[loop_len:loop_len + xfade]
    if len(spill) == xfade:
        body[:xfade] += spill * np.linspace(1, 0, xfade)
    # 頭と尻に極短いフェードでクリック防止
    fe = int(0.01 * SR)
    body[:fe] *= np.linspace(0, 1, fe)
    body[-fe:] *= np.linspace(1, 0, fe)

    peak = np.max(np.abs(body)) or 1.0
    body = body / peak * 0.72                     # -2.8 dBFS くらい。うるさくしない
    # soft clip
    body = np.tanh(body * 1.1) / np.tanh(1.1)
    body = (body * 32767).astype("<i2")

    with wave.open(WAV_PATH, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(body.tobytes())
    return loop_len / SR


if __name__ == "__main__":
    mid, total = build_midi()
    loop_sec = render_wav(mid, total)
    print(f"MIDI : {MID_PATH}")
    print(f"WAV  : {WAV_PATH}  ({loop_sec:.1f}s loop, {BPM} BPM)")
