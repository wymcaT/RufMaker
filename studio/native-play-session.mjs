import fs from 'node:fs';
import {spawn} from 'node:child_process';
import {readNativeStatus,waitForNativeReplay} from './native-replay.mjs';

export class NativePlaySession {
  constructor({result,target,cancel=new AbortController(),onProgress=()=>{},launch=spawn,wait=waitForNativeReplay,pollMs=150}) {
    Object.assign(this,{result,target,cancel,onProgress,launch,wait,pollMs});this.command=fs.readFileSync(result.commandFile,'utf8');this.closed=false;this.restarting=false;this.timer=null;this.child=null;
  }
  async launchAtTarget() {
    this.cancel.signal.throwIfAborted();fs.rmSync(this.result.statusFile,{force:true});fs.writeFileSync(this.result.commandFile,this.command);
    this.child=this.launch(this.result.exe,[],{cwd:this.result.dir,detached:true,windowsHide:true,stdio:'ignore'});
    await new Promise((resolve,reject)=>{this.child.once('spawn',resolve);this.child.once('error',reject);});this.child.unref();
    const status=await this.wait({child:this.child,statusFile:this.result.statusFile,target:this.target,dir:this.result.dir,signal:this.cancel.signal,onProgress:p=>this.onProgress({...p,phase:this.restarting?'restart':'replay'})});
    this.onProgress({phase:'ready',frame:this.target,target:this.target,room:status.room||''});return status;
  }
  async start() {
    const status=await this.launchAtTarget();this.timer=setInterval(()=>{this.poll().catch(()=>{});},this.pollMs);this.timer.unref();return status;
  }
  async stopChild() {
    const child=this.child;if(!child||child.exitCode!==null||child.signalCode)return;
    await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(new Error('上一次原生试玩尚未退出。')),3000);child.once('close',()=>{clearTimeout(timer);resolve();});if(!child.kill()){clearTimeout(timer);resolve();}});
  }
  async poll() {
    if(this.closed||this.restarting)return;
    try {
      const status=readNativeStatus(this.result.statusFile);
      if(Number(status.restart)===1){
        this.restarting=true;this.onProgress({phase:'restart',frame:0,target:this.target,room:''});await this.stopChild();await this.launchAtTarget();this.restarting=false;
      }else if(this.child.exitCode!==null||this.child.signalCode){this.closed=true;clearInterval(this.timer);this.onProgress({phase:'ended'});}
    }catch(error){await this.stopChild().catch(()=>{});this.closed=true;clearInterval(this.timer);this.onProgress({phase:this.cancel.signal.aborted?'ended':'error',error:error.message});}
  }
  async stop() {this.closed=true;this.cancel.abort();clearInterval(this.timer);await this.stopChild();}
}
