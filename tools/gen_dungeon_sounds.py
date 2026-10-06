"""Generate only dungeon warning/seal cues; preserve existing sound files."""
import math
from pathlib import Path
import struct
import wave

RATE = 44100
OUT = Path(__file__).resolve().parents[1] / 'GravityMaze' / 'Sounds'

def save(name, duration, notes):
    samples = []
    for i in range(int(duration * RATE)):
        t = i / RATE
        value = 0.0
        for start, frequency, decay in notes:
            age = t - start
            if age >= 0:
                envelope = min(1.0, age / 0.004) * math.exp(-age * decay)
                value += envelope * (math.sin(math.tau * frequency * age) + 0.2 * math.sin(math.tau * frequency * 2.7 * age))
        samples.append(value)
    peak = max(abs(v) for v in samples) or 1
    with wave.open(str(OUT / (name + '.wav')), 'wb') as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(RATE)
        audio.writeframes(b''.join(struct.pack('<h', round(v / peak * 24000)) for v in samples))

save('dungeon_warning', 0.28, [(0, 960, 32), (0.08, 720, 28)])
save('dungeon_seal', 0.85, [(0, 523.25, 6), (0.12, 659.25, 6), (0.24, 783.99, 5), (0.36, 1046.5, 5)])
