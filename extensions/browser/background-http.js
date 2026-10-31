importScripts('connection.js');
const browserKind=navigator.userAgent.includes('YaBrowser')?'Yandex':navigator.userAgent.includes('Edg/')?'Edge':'Chrome';
const sources=new Map(),relayFrames=new Set(),mainFrames=new Set();
const diagnostic={version:'1.3.0',connected:false,relayFrames:0,mainFrames:0,players:0,error:''};
let polling=false,lastPush=0;
function active(){const now=Date.now();for(const [key,item] of sources)if(now-item.updated>15000)sources.delete(key);return [...sources.values()].sort((a,b)=>Number(b.playing)-Number(a.playing)||b.activatedAt-a.activatedAt)[0];}
async function request(path,body){
  const response=await fetch(ISLAND_ENDPOINT+path,{method:body?'POST':'GET',headers:{Authorization:'Bearer '+ISLAND_TOKEN,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined,cache:'no-store',credentials:'omit',signal:AbortSignal.timeout(1500)});
  if(!response.ok)throw Error('HTTP '+response.status);
  diagnostic.connected=true;diagnostic.error='';return response.json();
}
function failed(error){diagnostic.connected=false;diagnostic.error=error.message;}
function sendState(force=false){const item=active();if(!item || !force && Date.now()-lastPush<200)return;lastPush=Date.now();request('/state',item).catch(failed);}
async function poll(){
  if(polling)return;polling=true;
  try{const result=await request('/commands/'+browserKind);for(const command of result.commands || []){const item=sources.get(command.sourceKey);if(item)chrome.tabs.sendMessage(item.tabId,{type:'media-command',action:command.action,value:command.value},{frameId:item.frameId},()=>{void chrome.runtime.lastError;});}}
  catch(error){failed(error);}finally{polling=false;}
}
chrome.runtime.onMessage.addListener((message,sender,respond)=>{
  if(message.type==='bridge-diagnostic'){respond({...diagnostic,players:sources.size});return;}
  if(message.type==='bridge-reconnect'){request('/ping').then(()=>{sendState(true);respond({...diagnostic,players:sources.size});}).catch(error=>{failed(error);respond({...diagnostic,players:sources.size});});return true;}
  if(message.type==='bridge-relay-ready'){relayFrames.add(sender.tab?.id+':'+sender.frameId);diagnostic.relayFrames=relayFrames.size;return;}
  if(message.type==='bridge-main-ready'){mainFrames.add(sender.tab?.id+':'+sender.frameId);diagnostic.mainFrames=mainFrames.size;return;}
  if(message.type==='bridge-relay-heartbeat'){const key=sender.tab?.id+':'+sender.frameId;relayFrames.add(key);if(message.mainReady)mainFrames.add(key);diagnostic.relayFrames=relayFrames.size;diagnostic.mainFrames=mainFrames.size;diagnostic.mediaElements=message.mediaElements;poll();return;}
  if(message.type!=='media-state' || !sender.tab || !message.payload)return;
  const p=message.payload;if(typeof p.playing!=='boolean'||!Number.isFinite(p.position))return;
  const key=browserKind+':'+sender.tab.id+':'+sender.frameId;
  const previous=sources.get(key);
  sources.set(key,{sourceKey:key,tabId:sender.tab.id,frameId:sender.frameId,browser:browserKind,title:String(p.title||'Video or music').slice(0,200),artist:String(p.artist||'').slice(0,200),playing:p.playing,position:p.position,duration:Number.isFinite(p.duration)?p.duration:0,rate:Number.isFinite(p.rate)?p.rate:1,activatedAt:Number(p.activatedAt)||Date.now(),updated:Date.now()});
  sendState(!previous || previous.playing!==p.playing || Math.abs(previous.position-p.position)>2);
});
chrome.tabs.onRemoved.addListener(tabId=>{for(const [key,item] of sources)if(item.tabId===tabId)sources.delete(key);});
setInterval(poll,500);poll();
