"""Summarize real Editor DSP observations; never synthesize playback evidence."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = root / 'docs/validation/m9'
data = json.loads((folder / 'audio-events.json').read_text(encoding='utf-8-sig'))
assert data['status'] == 'complete' and all(check['passed'] for check in data['checks'])
events = data['events']
start = events[0]['AtSeconds']
lines = [
    '# M9 BGM 実再生記録', '',
    f"Unity Editorの実時間 {data['elapsedSeconds']:.3f} 秒で {len(data['checks'])} 項目すべて成功。",
    'イントロ/ループを実際のAudioSourceで再生し、DSP時刻・timeSamples・isPlaying・ミキサー音量を観測した。',
    'Android実機での聴取結果ではない。背景停止はOnApplicationPause(true/false)へ同じ処理を送ったEditor検証。', '',
    'ゲームは最新UI_SPEC17章の指定曲、ホームは和風/バンド。ゲームから開くHelpはゲーム曲を継続する。', '',
    '| 開始から秒 | 画面/文脈 | イベント | 曲 | フェード秒 | BGM音量 |',
    '|---:|---|---|---|---:|---:|',
]
for event in events:
    key = event['ObservedTrackKey'] or event['TargetKey'] or '停止'
    volume = event['Volume'] * 100 if event['SoundEnabled'] else 0
    lines.append(f"| {event['AtSeconds'] - start:.3f} | {event['Context']} | {event['Kind']} | {key} | {event['FadeSeconds']:g} | {volume:g}% |")
lines += ['', '## 検証項目', '', '| 完了秒 | 項目 | 結果 |', '|---:|---|---|']
for check in data['checks']:
    lines.append(f"| {check['elapsedSeconds']:.3f} | {check['step']} | 成功 |")
lines += ['', '詳細な予約DSP時刻・実測サンプル位置・各Voiceのgainは`audio-events.json`、件数は`audio-result.json`。',
          '音量の永続化は既存のProfileData/LocalAccountsを使用し、今回のCore全548件にその回帰テストも含む。', '']
(folder / 'audio.md').write_text('\n'.join(lines), encoding='utf-8')
