import fs from 'node:fs';
import path from 'node:path';

export function resolveGameMaker(root,environment=process.env) {
  let saved='';const preferences=path.join(root,'user-data','settings.json');
  if(fs.existsSync(preferences))saved=JSON.parse(fs.readFileSync(preferences,'utf8')).gameMaker||'';
  const candidates=[environment.RUFMAKER_GAMEMAKER,saved,path.join(root,'GameMaker8.2','GameMaker.exe'),path.join(environment.APPDATA||'','GameMaker8.2','GameMaker.exe')].filter(Boolean);
  return candidates.find(file=>fs.existsSync(file)&&fs.statSync(file).isFile())||path.join(root,'GameMaker8.2','GameMaker.exe');
}
