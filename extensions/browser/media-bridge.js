(() => {
  'use strict';
  if (window.__islandMediaBridge) return;
  window.__islandMediaBridge = true;
  const session = navigator.mediaSession || {metadata:null,playbackState:'none',setActionHandler(){},setPositionState(){}};
  const watched = new WeakSet();
  let current = null;
  let lastPlayback = '';
  let lastPosition = -1;
  let lastDuration = -1;
  let generatedMetadata = null;
  let lastTitle = '';
  let lastBindAt = 0;
  let activatedAt = 0;

  function findPlaying() {
    return [...document.querySelectorAll('audio,video')].find(m => !m.paused && !m.ended && m.readyState > 0);
  }
  function seek(position) {
    if (!current || !Number.isFinite(current.duration) || current.duration <= 0) return;
    const start = current.seekable.length ? current.seekable.start(0) : 0;
    const end = current.seekable.length ? current.seekable.end(current.seekable.length - 1) : current.duration;
    current.currentTime = Math.max(start, Math.min(end, position));
    publish(true);
  }
  function bindControls() {
    const handlers = {
      play: () => { if (current) current.play().catch(() => {}); },
      pause: () => { if (current) current.pause(); },
      seekto: details => seek(details.seekTime),
      seekbackward: details => { if (current) seek(current.currentTime - (details.seekOffset || 10)); },
      seekforward: details => { if (current) seek(current.currentTime + (details.seekOffset || 10)); }
    };
    for (const [action, handler] of Object.entries(handlers)) {
      try { session.setActionHandler(action, handler); } catch (_) { /* Older browser. */ }
    }
  }
  function publish(force = false) {
    if (!current) {
      current = findPlaying();
      if (!current) return;
      force = true;
    }
    // Keep the site's own title, artist and cover when it provides them.
    const title = document.title.trim() || 'Видео или музыка';
    if (!session.metadata || session.metadata === generatedMetadata) {
      if (force || lastTitle !== title) {
        generatedMetadata = typeof MediaMetadata==='function'?new MediaMetadata({title}):{title};
        session.metadata = generatedMetadata;
        lastTitle = title;
      }
    }
    const playback = current.paused || current.ended ? 'paused' : 'playing';
    if (session.playbackState !== playback || force) {
      session.playbackState = playback;
      lastPlayback = playback;
    }
    const duration = current.duration;
    const position = current.currentTime;
    if (Number.isFinite(duration) && duration > 0 && Number.isFinite(position)) {
      if (force || Math.abs(position - lastPosition) >= 0.2 || duration !== lastDuration) {
        try {
          session.setPositionState({duration,position:Math.max(0,Math.min(duration,position)),playbackRate:current.playbackRate || 1});
          lastPosition = position;
          lastDuration = duration;
        } catch (_) { /* Live streams may have no seek range. */ }
      }
    } else {
      try { session.setPositionState(); } catch (_) {}
    }
    if (force || performance.now() - lastBindAt > 1500) {bindControls();lastBindAt=performance.now();}
    window.postMessage({channel:'win-dynamic-island-media',type:'state',payload:{title:session.metadata?.title || title,artist:session.metadata?.artist || '',playing:playback==='playing',position:Number.isFinite(position)?position:0,duration:Number.isFinite(duration)?duration:0,rate:current.playbackRate || 1,activatedAt}},'*');
  }
  function activate(media) {
    current = media;
    activatedAt = Date.now();
    publish(true);
  }
  function watch(media) {
    if (watched.has(media)) return;
    watched.add(media);
    media.addEventListener('play', () => activate(media));
    for (const name of ['pause','ended','seeked','ratechange','durationchange','loadedmetadata','timeupdate']) {
      media.addEventListener(name, () => { if (media === current) publish(name === 'seeked' || name === 'loadedmetadata'); });
    }
    if (!media.paused && !media.ended) activate(media);
  }
  function scan(root) {
    if (root.matches?.('audio,video')) watch(root);
    root.querySelectorAll?.('audio,video').forEach(watch);
  }
  scan(document);
  new MutationObserver(records => {
    for (const record of records) for (const node of record.addedNodes) {
      if (node.nodeType === 1) scan(node);
    }
  }).observe(document,{childList:true,subtree:true});
  // VK and other players can use detached new Audio() objects or shadow DOM.
  // Observe their own play calls as well as ordinary document media elements.
  const originalPlay = HTMLMediaElement.prototype.play;
  HTMLMediaElement.prototype.play = function (...args) {
    watch(this);
    return Reflect.apply(originalPlay,this,args);
  };
  const originalCreateElement=Document.prototype.createElement;
  Document.prototype.createElement=function(...args){const element=Reflect.apply(originalCreateElement,this,args);if(element instanceof HTMLMediaElement)watch(element);return element;};
  const OriginalAudio=window.Audio;
  if(OriginalAudio)window.Audio=new Proxy(OriginalAudio,{construct(target,args,newTarget){const media=Reflect.construct(target,args,newTarget);watch(media);return media;},apply(target,self,args){const media=Reflect.apply(target,self,args);watch(media);return media;}});
  document.addEventListener('play', event => {
    if (event.target instanceof HTMLMediaElement) {watch(event.target);activate(event.target);}
  },true);
  setInterval(() => {
    if (current && !current.ended) publish();
  },500);
  window.addEventListener('message',event => {
    const message=event.data;
    if(event.source===window && message?.channel==='win-dynamic-island-media' && message.type==='probe'){window.postMessage({channel:'win-dynamic-island-media',type:'ready'},'*');return;}
    if(event.source!==window || message?.channel!=='win-dynamic-island-media' || message.type!=='command' || !current)return;
    if(message.action==='toggle'){if(current.paused)current.play().catch(()=>{});else current.pause();}
    else if(message.action==='seek' && Number.isFinite(message.value))seek(message.value);
  });
  window.postMessage({channel:'win-dynamic-island-media',type:'ready'},'*');
})();
