const ru=chrome.i18n.getUILanguage().startsWith('ru');
const words=ru?{online:'Связь с островом установлена.',offline:'Нет связи с островом.',players:'Плееров: ',retry:'Подключиться заново',copy:'Скопировать диагностику',copied:'Скопировано'}:{online:'Connected to the island.',offline:'No connection to the island.',players:'Players: ',retry:'Reconnect',copy:'Copy diagnostics',copied:'Copied'};
document.querySelector('#retry').textContent=words.retry;document.querySelector('#copy').textContent=words.copy;
let diagnostic={};
function update(response){diagnostic=response || {error:chrome.runtime.lastError?.message || 'No background response'};document.querySelector('#status').textContent=(diagnostic.connected?words.online:words.offline)+' '+words.players+(diagnostic.players || 0)+'.';}
chrome.runtime.sendMessage({type:'bridge-reconnect'},update);
document.querySelector('#retry').addEventListener('click',()=>chrome.runtime.sendMessage({type:'bridge-reconnect'},update));
document.querySelector('#copy').addEventListener('click',async()=>{await navigator.clipboard.writeText(JSON.stringify(diagnostic));document.querySelector('#copy').textContent=words.copied;});
