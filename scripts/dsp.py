"""共有 DSP: 手続き生成の音を柔らかくするためのフィルタ / リバーブ。"""
import numpy as np

SR = 44100


def one_pole_lp(x, cutoff):
    dt = 1.0 / SR
    rc = 1.0 / (2 * np.pi * cutoff)
    a = dt / (rc + dt)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def one_pole_hp(x, cutoff):
    return x - one_pole_lp(x, cutoff)


def fft_convolve(a, b):
    n = len(a) + len(b) - 1
    N = 1 << (n - 1).bit_length()
    A = np.fft.rfft(a, N)
    B = np.fft.rfft(b, N)
    return np.fft.irfft(A * B, N)[:n]


def make_ir(decay=0.42, length=1.7, lp=3400.0, seed=7):
    """指数減衰ノイズの簡易インパルス応答(プレート風)。"""
    rng = np.random.default_rng(seed)
    n = int(length * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-t / decay)
    ir = one_pole_lp(ir, lp)
    pre = int(0.004 * SR)
    ir[:pre] *= np.linspace(0, 1, pre)          # プレリング抑制
    e = np.sqrt((ir ** 2).sum()) or 1.0
    return ir / e


def reverb(x, ir, mix=0.26):
    wet = fft_convolve(x, ir)[:len(x)]
    p = np.max(np.abs(wet)) or 1.0
    wet = wet / p * (np.max(np.abs(x)) or 1.0)
    return (1.0 - mix) * x + mix * wet


def soft_limit(x, drive=1.15):
    return np.tanh(x * drive) / np.tanh(drive)


def normalize(x, peak=0.9):
    p = np.max(np.abs(x)) or 1.0
    return x / p * peak


def write_wav(path, mono, gain=0.9):
    import wave
    data = (soft_limit(normalize(mono, 0.97)) * 32767 * gain).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12.0)
