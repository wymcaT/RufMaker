import fs from 'node:fs';
import path from 'node:path';
import {setTimeout as sleep} from 'node:timers/promises';

export function readNativeStatus(file) {
  try {
    return Object.fromEntries(fs.readFileSync(file,'utf8').split(/\r?\n/).filter(line=>line.includes('=')).map(line=>{
      const at=line.indexOf('=');return [line.slice(0,at).trim(),line.slice(at+1).trim().replace(/^"|"$/g,'')];
    }));
  } catch(error) {
    if(!['ENOENT','EBUSY','EPERM','EACCES'].includes(error.code))throw error;
    return {};
  }
}

// Native drawing can be slower than real time. Only a lack of frame progress is a timeout.
export async function waitForNativeReplay({child,statusFile,target,dir,signal,onProgress=()=>{},stallMs=60000,pollMs=100,now=Date.now,delay=sleep}) {
  let highest=-1,lastProgress=now();
  for(;;) {
    signal?.throwIfAborted();
    if(child.exitCode!==null||child.signalCode)throw new Error('原生试玩在重放阶段退出，请查看 '+path.join(dir,'game_errors.log')+'。');
    const status=readNativeStatus(statusFile),frame=status.frame!==undefined&&status.frame.trim()!==''?Number(status.frame):NaN;
    if(Number.isFinite(frame)&&frame>=0) {
      if(frame>highest){highest=frame;lastProgress=now();}
      onProgress({frame:Math.min(target,highest),target,room:status.room||''});
      if(Number(status.ready)===1&&frame>=target)return status;
    }
    if(now()-lastProgress>=stallMs)throw new Error('原生试玩连续 '+Math.round(stallMs/1000)+' 秒没有前进（第 '+Math.max(0,highest)+' / '+target+' 帧）；请检查游戏报错或等待输入的代码。记录目录：'+dir);
    await delay(pollMs,undefined,{signal});
  }
}
