// Waveforms belong to the selected project/song, never a canned song-name fallback.
import fs from 'node:fs';
import path from 'node:path';
export function readProjectWaveform(project, music) {
  const cache = path.join(project, 'cache', 'rufmaker-waveform.json');
  if (fs.existsSync(cache)) {
    const data = JSON.parse(fs.readFileSync(cache, 'utf8'));
    if (data.source === music) return data;
  }
  return {duration: 0, peaks: []};
}
