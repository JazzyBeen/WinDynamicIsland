let alive=true;
let mainReady=false;
function send(message){if(!alive)return;try{chrome.runtime.sendMessage(message,()=>{void chrome.runtime.lastError;});}catch(_){alive=false;}}
send({type:'bridge-relay-ready'});
window.addEventListener('message',event => {
  if(event.source!==window || event.data?.channel!=='win-dynamic-island-media')return;
  if(event.data.type==='ready'){mainReady=true;send({type:'bridge-main-ready'});return;}
  if(event.data.type!=='state')return;
  mainReady=true;
  const p=event.data.payload;
  if(!p || typeof p.playing!=='boolean' || !Number.isFinite(p.position))return;
  send({type:'media-state',payload:p});
});
setInterval(()=>{window.postMessage({channel:'win-dynamic-island-media',type:'probe'},'*');send({type:'bridge-relay-heartbeat',mainReady,mediaElements:document.querySelectorAll('audio,video').length});},2500);
chrome.runtime.onMessage.addListener(message => {
  if(message.type==='media-command')window.postMessage({channel:'win-dynamic-island-media',type:'command',action:message.action,value:message.value},'*');
});
