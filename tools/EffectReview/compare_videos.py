"""Encode genuine Unity Game-view captures and align the supplied reference clips.

Usage: python tools/EffectReview/compare_videos.py --ffmpeg PATH_TO_FFMPEG
The left half is the supplied mock; the right half is the Unity recording.
"""
import argparse
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg", required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    videos = root / "design/videos"
    captured = videos / "unity"
    review = json.loads((root / "Assets/Imoni/Config/GameConfig.json").read_text(encoding="utf-8"))["effectReview"]
    fps = review["frameRate"]
    summary = {}
    for name, offset in (("imoni_success", 0.76), ("imoni_fail", 0.24)):
        frames = captured / (name + "_frames")
        records = json.loads((captured / (name + "_telemetry.json")).read_text(encoding="utf-8"))
        if len(list(frames.glob("*.png"))) != len(records):
            raise RuntimeError("Frame/telemetry count mismatch: " + name)
        run = lambda command: subprocess.run([args.ffmpeg, "-hide_banner", "-loglevel", "error", "-y"] + command, check=True)
        target = captured / (name + ".mp4")
        run(["-framerate", str(fps), "-i", str(frames / "%05d.png"), "-frames:v", str(len(records)),
             "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(target)])
        comparison = captured / (name + "_comparison.mp4")
        run(["-i", str(videos / (name + ".mp4")), "-i", str(target),
             "-filter_complex", f"[0:v]trim=start={offset},setpts=PTS-STARTPTS[reference];[1:v]setpts=PTS-STARTPTS[unity];[reference][unity]hstack=inputs=2:shortest=1[out]",
             "-map", "[out]", "-an", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(comparison)])
        for second in (3.4, 5.5, 8.0, 9.5):
            run(["-ss", str(second), "-i", str(comparison), "-frames:v", "1", str(captured / (name + f"_compare_{second:.1f}s.png"))])
        levels = {}
        for record in records:
            if record["level"] > 0:
                levels.setdefault(str(record["level"]), record["timeMs"])
        finale = next(record["finaleAtMs"] for record in records if record["finaleAtMs"] is not None)
        button = next(record["timeMs"] for record in records if record["resultVisible"])
        summary[name] = {"size": [review["width"], review["height"]], "fps": fps,
                         "frames": len(records), "referenceLeadingBlankTrimSec": offset,
                         "levelFirstVisibleMs": levels, "finaleAtMs": finale,
                         "resultFirstVisibleMs": button, "withinTenSeconds": button <= 10000}
    (captured / "comparison-metrics.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
